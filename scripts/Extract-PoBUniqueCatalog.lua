#@ SimpleGraphic
-- Evaluates Path of Building's own Unique data modules with its bundled runtime.

local outputPath = os.getenv("POENHANCE_POB_UNIQUE_OUTPUT")
if not outputPath or outputPath == "" then
	error("POENHANCE_POB_UNIQUE_OUTPUT must name the output JSON file")
end

launch = { devMode = false }
SetMainObject(launch)

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

local function extract()
	LoadModule("GameVersions")
	LoadModule("Modules/Common")
	LoadModule("Modules/CalcFormat")
	LoadModule("Modules/Data")

	local entries = { }
	for uniqueType, rawItems in pairs(data.uniques) do
		for _, rawItem in ipairs(rawItems) do
			table.insert(entries, {
				uniqueType = uniqueType,
				sourcePath = sourcePathFor(uniqueType),
				generated = uniqueType == "generated",
				raw = rawItem,
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
