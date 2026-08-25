#@ SimpleGraphic
-- Evaluates Path of Building's own Unique data modules with its bundled runtime.

local outputPath = os.getenv("POENHANCE_POB_UNIQUE_OUTPUT")
if not outputPath or outputPath == "" then
	error("POENHANCE_POB_UNIQUE_OUTPUT must name the output JSON file")
end

launch = { devMode = false, defaultItemAffixQuality = 0.5 }
SetMainObject(launch)
main = launch

local semanticEvidenceMethod = "pob-item-context-v1"

local function sourcePathFor(uniqueType)
	if uniqueType == "generated" then
		return "Data/Uniques/Special/Generated.lua"
	elseif uniqueType == "new" then
		return "Data/Uniques/Special/New.lua"
	elseif uniqueType == "race" then
		return "Data/Uniques/Special/race.lua"
	end
	return "Data/Uniques/" .. uniqueType .. ".lua"
end

local function hasSlotAttackCondition(mod)
	for _, tag in ipairs(mod) do
		if tag.type == "Condition" and
			(tag.var == "MainHandAttack" or tag.var == "OffHandAttack") then
			return true
		end
	end
	return false
end

local function classifyMod(before, after)
	if not after then
		return "local"
	end
	if not hasSlotAttackCondition(before) and hasSlotAttackCondition(after) then
		return "local"
	end
	return "global"
end

local function classifyLineLocality(item, modLine, lineIndex)
	if not item.base or not modLine.modList or #modLine.modList == 0 then
		return "unknown"
	end

	local marker = "PoEnhanceSemantic:" .. tostring(lineIndex) .. ":"
	local isolated = new("ModList")
	local beforeBySource = { }
	for modIndex, mod in ipairs(modLine.modList) do
		local clone = copyTable(mod)
		clone.source = marker .. tostring(modIndex)
		beforeBySource[clone.source] = clone
		isolated:AddMod(clone)
	end

	if item.base.weapon then
		item.weaponData = { }
	elseif item.base.armour then
		item.armourData = { }
	end
	local contextual = item:BuildModListForSlotNum(isolated, 1)
	local afterBySource = { }
	for _, mod in ipairs(contextual) do
		if type(mod.source) == "string" and mod.source:sub(1, #marker) == marker then
			afterBySource[mod.source] = mod
		end
	end

	local sawLocal = false
	local sawGlobal = false
	for source, before in pairs(beforeBySource) do
		if classifyMod(before, afterBySource[source]) == "local" then
			sawLocal = true
		else
			sawGlobal = true
		end
	end
	if sawLocal and sawGlobal then
		return "mixed"
	elseif sawLocal then
		return "local"
	elseif sawGlobal then
		return "global"
	end
	return "unknown"
end

local function fingerprintsForItem(item)
	if not item.baseName then
		return { }
	end
	local result = { }
	local function append(kind, modLines)
		for index, modLine in ipairs(modLines or { }) do
			local localityOk, locality = pcall(
				classifyLineLocality,
				item,
				modLine,
				index)
			table.insert(result, {
				kind = kind,
				lineIndex = index - 1,
				line = modLine.line,
				baseType = item.baseName,
				locality = localityOk and locality or "unknown",
				evidenceMethod = semanticEvidenceMethod,
			})
		end
	end
	append("implicit", item.implicitModLines)
	append("unique", item.explicitModLines)
	return result
end

local function semanticFingerprintsFor(raw)
	local ok, fingerprints = pcall(function()
		local defaultItem = new("Item", raw, "UNIQUE", true)
		local items = { defaultItem }
		local seenBaseTypes = { [defaultItem.baseName or ""] = true }
		local variantIndices = { }
		local versionIndices = { }
		for _, baseLine in pairs(defaultItem.baseLines or { }) do
			for index in pairs(baseLine.variantList or { }) do
				variantIndices[index] = true
			end
			for index in pairs(baseLine.versionList or { }) do
				versionIndices[index] = true
			end
		end
		local rawWithoutSelection = raw:gsub("\nSelected Variant:[^\n]*", "")
		for index in pairsSortByKey(variantIndices) do
			local item = new("Item")
			item.variant = index
			item:ParseRaw(rawWithoutSelection, "UNIQUE", true)
			if item.baseName and not seenBaseTypes[item.baseName] then
				seenBaseTypes[item.baseName] = true
				table.insert(items, item)
			end
		end
		local rawWithoutVersion = raw:gsub("\nSelected Version:[^\n]*", "")
		for index in pairsSortByKey(versionIndices) do
			local item = new("Item")
			item.selectedVersion = index
			item:ParseRaw(rawWithoutVersion, "UNIQUE", true)
			if item.baseName and not seenBaseTypes[item.baseName] then
				seenBaseTypes[item.baseName] = true
				table.insert(items, item)
			end
		end

		local result = { }
		for _, item in ipairs(items) do
			for _, fingerprint in ipairs(fingerprintsForItem(item)) do
				table.insert(result, fingerprint)
			end
		end
		table.sort(result, function(left, right)
			if left.kind ~= right.kind then
				return left.kind < right.kind
			elseif left.lineIndex ~= right.lineIndex then
				return left.lineIndex < right.lineIndex
			elseif left.baseType ~= right.baseType then
				return left.baseType < right.baseType
			elseif left.locality ~= right.locality then
				return left.locality < right.locality
			end
			return left.line < right.line
		end)
		return result
	end)
	return ok and fingerprints or { }
end

local function optionAxesFor(raw)
	local ok, axes = pcall(function()
		local item = new("Item", raw, "UNIQUE", true)
		if item.usesVariantGroups or not item.variantList or not item.hasAltVariant then
			return { }
		end

		local selectionLimit = 1
		local selectedChoiceIndices = { item.variant }
		for index = 1, 5 do
			local suffix = index == 1 and "" or tostring(index)
			if item["hasAltVariant" .. suffix] then
				selectionLimit = selectionLimit + 1
				table.insert(selectedChoiceIndices, item["variantAlt" .. suffix])
			end
		end

		local sourceChoiceIndices = { }
		for index = 1, #item.variantList do
			table.insert(sourceChoiceIndices, index)
		end
		return {
			{
				sourceKind = "legacySharedVariantSelection",
				sourceOrdinal = 1,
				selectionLimit = selectionLimit,
				sourceChoiceIndices = sourceChoiceIndices,
				selectedChoiceIndices = selectedChoiceIndices,
			}
		}
	end)
	return ok and axes or { }
end

local function extract()
	LoadModule("GameVersions")
	LoadModule("Modules/Common")
	LoadModule("Modules/CalcFormat")
	LoadModule("Modules/Data")
	LoadModule("Modules/ModTools")
	LoadModule("Modules/ItemTools")

	-- PoB appends passive-tree-dependent generated Uniques from the PassiveTree
	-- constructor. Evaluate the same pinned current-tree path before enumerating the
	-- catalog so those source definitions retain their generated option axes.
	local originalNewImageHandle = NewImageHandle
	local originalMain = main
	NewImageHandle = function()
		return {
			Load = function() end,
			ImageSize = function() return 4096, 4096 end,
		}
	end
	main = launch
	data.setJewelRadiiGlobally(latestTreeVersion)
	new("PassiveTree", latestTreeVersion)
	NewImageHandle = originalNewImageHandle
	main = originalMain

	local entries = { }
	for uniqueType, rawItems in pairs(data.uniques) do
		for _, rawItem in ipairs(rawItems) do
			table.insert(entries, {
				uniqueType = uniqueType,
				sourcePath = sourcePathFor(uniqueType),
				generated = uniqueType == "generated",
				raw = rawItem,
				semanticFingerprints = semanticFingerprintsFor(rawItem),
				optionAxes = optionAxesFor(rawItem),
			})
		end
	end

	table.sort(entries, function(left, right)
		if left.sourcePath ~= right.sourcePath then
			return left.sourcePath < right.sourcePath
		end
		return left.raw < right.raw
	end)

	local file, openError = io.open(outputPath, "wb")
	if not file then
		error("Unable to open output file: " .. tostring(openError))
	end
	file:write(require("dkjson").encode({ entries = entries }, { indent = true }))
	file:write("\n")
	file:close()
	ConPrintf("PoEnhance Unique extraction wrote %d evaluated entries", #entries)
end

function launch:OnInit()
	local succeeded, failure = xpcall(extract, debug.traceback)
	if not succeeded then
		local file = io.open(outputPath, "wb")
		if file then
			file:write(require("dkjson").encode({ error = tostring(failure) }, { indent = true }))
			file:write("\n")
			file:close()
		end
		self.completed = true
		Exit()
		return
	end
	self.completed = true
	Exit()
end

function launch:OnFrame()
	if self.completed then
		Exit()
	end
end
function launch:CanExit() return true end
function launch:OnExit() end
