[CmdletBinding()]
param(
    [Parameter(Mandatory)] [string]$CandidatePath,
    [Parameter(Mandatory)] [string]$OutputDirectory,
    [string]$BaselineCandidatePath
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$candidate = [System.IO.Path]::GetFullPath($CandidatePath)
$outputRoot = [System.IO.Path]::GetFullPath($OutputDirectory)
$baselineCandidate = if ([string]::IsNullOrWhiteSpace($BaselineCandidatePath))
{
    $null
}
else
{
    [System.IO.Path]::GetFullPath($BaselineCandidatePath)
}
if (-not (Test-Path -LiteralPath $candidate -PathType Leaf))
{
    throw "Candidate package does not exist: $candidate"
}
[System.IO.Directory]::CreateDirectory($outputRoot) | Out-Null

$package = Get-Content -LiteralPath $candidate -Raw | ConvertFrom-Json
if ($null -eq $package.uniqueItems)
{
    throw 'Candidate package does not contain a Unique catalog.'
}
if ($null -ne $baselineCandidate -and -not (Test-Path -LiteralPath $baselineCandidate -PathType Leaf))
{
    throw "Baseline candidate package does not exist: $baselineCandidate"
}

$sourceById = @{}
foreach ($source in $package.uniqueItems.sourceObservations)
{
    $sourceById[$source.id] = $source
}

$items = @()
$conflicts = @()
$allVersions = @($package.uniqueItems.items | ForEach-Object { $_.versions })
$allBlocks = @($allVersions | ForEach-Object { $_.modifierBlocks })
$uniqueBlocks = @($allBlocks | Where-Object { $_.kind -eq 'unique' })
$fingerprintedBlocks = @($allBlocks | Where-Object { $null -ne $_.sourceSemanticFingerprint })
$sourceFingerprintLocalityCounts = [ordered]@{}
foreach ($locality in @('global', 'local', 'mixed', 'unknown'))
{
    $sourceFingerprintLocalityCounts[$locality] = @($fingerprintedBlocks | Where-Object {
        $_.sourceSemanticFingerprint.locality -eq $locality
    }).Count
}
$matchedFingerprintBlocks = @($allBlocks | Where-Object {
    $provenanceProperty = $_.mechanicalMapping.PSObject.Properties['provenance']
    $null -ne $provenanceProperty -and
    $null -ne $provenanceProperty.Value.matchedSemanticFingerprint
})
$comparableMatchedFingerprintBlocks = @($matchedFingerprintBlocks | Where-Object {
    $fingerprint = $_.mechanicalMapping.PSObject.Properties['provenance'].Value.matchedSemanticFingerprint
    $fingerprint.locality -ne 'unknown' -and
    @($fingerprint.orderedStatIds).Count -gt 0 -and
    $fingerprint.valueShape -ne 'unknown' -and
    @($fingerprint.values).Count -gt 0
})
$multipleSemanticKeyObservationIds = @($allBlocks | Where-Object {
    $_.mechanicalMapping.status -eq 'ambiguous' -and
    $_.mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_EXACT_CONFLICT'
} | ForEach-Object { $_.sourceObservationIds } | Sort-Object -Unique)
$blockContexts = @($package.uniqueItems.items | ForEach-Object {
    $item = $_
    $item.versions | ForEach-Object {
        $version = $_
        $version.modifierBlocks | Where-Object { $_.kind -eq 'unique' } | ForEach-Object {
            [pscustomobject]@{
                canonicalName = $item.canonicalName
                versionLabel = $version.label
                lines = @($_.lines)
                locality = $_.sourceSemanticFingerprint.locality
                status = $_.mechanicalMapping.status
                diagnosticCode = $_.mechanicalMapping.diagnosticCode
                modifierIds = @($_.mechanicalMapping.modifierIds)
                statIds = @($_.mechanicalMapping.statIds)
            }
        }
    }
})
$familyDefinitions = [ordered]@{
    attackSpeed = 'increased Attack Speed'
    energyShield = 'maximum Energy Shield'
    accuracy = 'Accuracy Rating'
    armourEvasion = '(Armour|Evasion Rating)'
    leech = 'Leeched as (Life|Mana)'
}
$semanticFamilyAudit = [ordered]@{}
foreach ($family in $familyDefinitions.GetEnumerator())
{
    $matches = @($blockContexts | Where-Object { (@($_.lines) -join "`n") -match $family.Value })
    $semanticFamilyAudit[$family.Key] = [ordered]@{
        pattern = $family.Value
        total = $matches.Count
        knownLocality = @($matches | Where-Object { $_.locality -ne 'unknown' }).Count
        resolved = @($matches | Where-Object {
            $_.status -eq 'exact' -or $_.status -eq 'equivalentSourceSet'
        }).Count
        exactConflict = @($matches | Where-Object {
            $_.diagnosticCode -eq 'UNIQUE_MECHANICS_EXACT_CONFLICT'
        }).Count
        samples = @($matches | Select-Object -First 8)
    }
}
foreach ($item in $package.uniqueItems.items)
{
    $blocks = @($item.versions | ForEach-Object { $_.modifierBlocks } | Where-Object { $_.kind -eq 'unique' })
    $resolved = @($blocks | Where-Object {
        $_.mechanicalMapping.status -eq 'exact' -or
        $_.mechanicalMapping.status -eq 'equivalentSourceSet'
    })
    $ambiguous = @($blocks | Where-Object { $_.mechanicalMapping.status -eq 'ambiguous' })
    $unsupported = @($blocks | Where-Object { $_.mechanicalMapping.status -eq 'unsupported' })
    $classification = if ($blocks.Count -eq 0) { 'IdentityOnly' }
        elseif ($resolved.Count -eq $blocks.Count) { 'FullyResolved' }
        elseif ($resolved.Count -gt 0) { 'PartiallyResolved' }
        else { 'UnsupportedModifiers' }
    $generated = @($item.sourceObservationIds | Where-Object {
        $sourceById.ContainsKey($_) -and $sourceById[$_].isGenerated
    }).Count -gt 0
    $items += [pscustomobject][ordered]@{
        identityId = $item.id
        canonicalName = $item.canonicalName
        kind = $item.kind
        generated = $generated
        classification = $classification
        versionCount = @($item.versions).Count
        currentVersionCount = @($item.versions | Where-Object { $_.role -eq 'current' }).Count
        historicalVersionCount = @($item.versions | Where-Object { $_.role -eq 'historical' }).Count
        uniqueBlockCount = $blocks.Count
        resolvedBlockCount = $resolved.Count
        ambiguousBlockCount = $ambiguous.Count
        unsupportedBlockCount = $unsupported.Count
        multiLineBlockCount = @($blocks | Where-Object { @($_.lines).Count -gt 1 }).Count
    }

    foreach ($version in $item.versions)
    {
        foreach ($block in @($version.modifierBlocks | Where-Object {
            $_.mechanicalMapping.status -eq 'ambiguous'
        }))
        {
            $conflicts += [ordered]@{
                identityId = $item.id
                canonicalName = $item.canonicalName
                versionId = $version.id
                versionLabel = $version.label
                blockId = $block.id
                lines = @($block.lines)
                modifierIds = @($block.mechanicalMapping.modifierIds)
                sourceObservationIds = @($block.sourceObservationIds)
                diagnosticCode = $block.mechanicalMapping.diagnosticCode
            }
        }
    }
}

$classificationCounts = [ordered]@{}
foreach ($group in $items | Group-Object classification | Sort-Object Name)
{
    $classificationCounts[$group.Name] = $group.Count
}
$mappingCounts = [ordered]@{}
foreach ($group in $uniqueBlocks | Group-Object { $_.mechanicalMapping.status } | Sort-Object Name)
{
    $mappingCounts[$group.Name] = $group.Count
}
$kindCounts = [ordered]@{}
foreach ($group in $items | Group-Object kind | Sort-Object Name)
{
    $kindCounts[$group.Name] = $group.Count
}
$unsupportedReasonCounts = [ordered]@{}
foreach ($group in @($uniqueBlocks | Where-Object {
    $_.mechanicalMapping.status -eq 'unsupported'
}) | Group-Object { $_.mechanicalMapping.diagnosticCode } | Sort-Object Name)
{
    $reason = if ([string]::IsNullOrWhiteSpace($group.Name)) { 'UNSPECIFIED' } else { $group.Name }
    $unsupportedReasonCounts[$reason] = $group.Count
}
$ambiguousReasonCounts = [ordered]@{}
foreach ($group in @($uniqueBlocks | Where-Object {
    $_.mechanicalMapping.status -eq 'ambiguous'
}) | Group-Object { $_.mechanicalMapping.diagnosticCode } | Sort-Object Name)
{
    $reason = if ([string]::IsNullOrWhiteSpace($group.Name)) { 'UNSPECIFIED' } else { $group.Name }
    $ambiguousReasonCounts[$reason] = $group.Count
}
$currentCapable = @($items | Where-Object { $_.currentVersionCount -gt 0 })
$historicalCapable = @($items | Where-Object { $_.historicalVersionCount -gt 0 })
$ordinaryCurrentCapable = @($currentCapable | Where-Object { $_.kind -eq 'ordinary' })
$identityReadyWithUnsupported = @($items | Where-Object {
    $_.classification -eq 'PartiallyResolved' -or $_.classification -eq 'UnsupportedModifiers'
})
$invalidIdentities = @($package.uniqueItems.items | Where-Object {
    [string]::IsNullOrWhiteSpace($_.id) -or
    [string]::IsNullOrWhiteSpace($_.canonicalName) -or
    @($_.baseTypeEvidence).Count -eq 0 -or
    @($_.versions).Count -eq 0
})
$baseChangeIdentities = @($package.uniqueItems.items | Where-Object {
    @($_.versions.baseType | Where-Object { -not [string]::IsNullOrWhiteSpace($_) } |
        Sort-Object -Unique).Count -gt 1
})
$ambiguousSafeHistoricalGroups = @()
foreach ($item in $package.uniqueItems.items)
{
    $historicalVersions = @($item.versions | Where-Object { $_.role -eq 'historical' })
    $groups = @($historicalVersions | Group-Object {
        $blockSignature = @($_.modifierBlocks | ForEach-Object {
            "$($_.kind):$(@($_.canonicalSignatures) -join [char]0x1f)"
        }) -join [char]0x1e
        "$($_.baseType)$([char]0x1d)$blockSignature"
    } | Where-Object { $_.Count -gt 1 })
    foreach ($group in $groups)
    {
        $ambiguousSafeHistoricalGroups += [ordered]@{
            identityId = $item.id
            versionIds = @($group.Group.id)
            versionLabels = @($group.Group.label)
        }
    }
}
$presenceOnlyCandidates = @($uniqueBlocks | Where-Object {
    (@($_.canonicalSignatures) -join "`n") -notmatch '<number>'
})
$scalarCandidates = @($uniqueBlocks | Where-Object {
    @($_.canonicalSignatures).Count -eq 1 -and
    [regex]::Matches([string]$_.canonicalSignatures[0], '<number>').Count -eq 1
})
$multiEffectCandidates = @($uniqueBlocks | Where-Object {
    @($_.lines).Count -gt 1 -or
    [regex]::Matches((@($_.canonicalSignatures) -join "`n"), '<number>').Count -gt 1
})
$generatedItems = @($items | Where-Object generated)
$generatedIdentityIds = @{}
foreach ($item in $generatedItems) { $generatedIdentityIds[$item.identityId] = $true }
$generatedUniqueBlocks = @($package.uniqueItems.items | Where-Object {
    $generatedIdentityIds.ContainsKey($_.id)
} | ForEach-Object { $_.versions } | ForEach-Object { $_.modifierBlocks } | Where-Object {
    $_.kind -eq 'unique'
})

$baselineComparison = $null
if ($null -ne $baselineCandidate)
{
    $baselinePackage = Get-Content -LiteralPath $baselineCandidate -Raw | ConvertFrom-Json
    if ($null -eq $baselinePackage.uniqueItems)
    {
        throw 'Baseline candidate package does not contain a Unique catalog.'
    }
    $baselineBlocks = @($baselinePackage.uniqueItems.items | ForEach-Object { $_.versions } |
        ForEach-Object { $_.modifierBlocks } | Where-Object { $_.kind -eq 'unique' })
    $baselineById = @{}
    foreach ($block in $baselineBlocks) { $baselineById[$block.id] = $block }
    $currentContextByBlockId = @{}
    foreach ($item in $package.uniqueItems.items)
    {
        foreach ($version in $item.versions)
        {
            foreach ($block in @($version.modifierBlocks | Where-Object { $_.kind -eq 'unique' }))
            {
                $currentContextByBlockId[$block.id] = [ordered]@{
                    canonicalName = $item.canonicalName
                    versionLabel = $version.label
                    lines = @($block.lines)
                }
            }
        }
    }
    $resolvedStatuses = @('exact', 'equivalentSourceSet')
    $notFoundMigrations = @($uniqueBlocks | Where-Object {
        $baselineById.ContainsKey($_.id) -and
        $baselineById[$_.id].mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_NOT_FOUND' -and
        $resolvedStatuses -contains $_.mechanicalMapping.status
    })
    $conflictMigrations = @($uniqueBlocks | Where-Object {
        $baselineById.ContainsKey($_.id) -and
        $baselineById[$_.id].mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_CONFLICT' -and
        $resolvedStatuses -contains $_.mechanicalMapping.status
    })
    $exactConflictMigrations = @($uniqueBlocks | Where-Object {
        $baselineById.ContainsKey($_.id) -and
        $baselineById[$_.id].mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_EXACT_CONFLICT' -and
        $resolvedStatuses -contains $_.mechanicalMapping.status
    })
    $currentById = @{}
    foreach ($block in $uniqueBlocks) { $currentById[$block.id] = $block }
    $resolvedRegressions = @($baselineBlocks | Where-Object {
        $resolvedStatuses -contains $_.mechanicalMapping.status -and
        $currentById.ContainsKey($_.id) -and
        $resolvedStatuses -notcontains $currentById[$_.id].mechanicalMapping.status
    })
    $baselineGeneratedSourceIds = @{}
    foreach ($source in @($baselinePackage.uniqueItems.sourceObservations | Where-Object isGenerated))
    {
        $baselineGeneratedSourceIds[$source.id] = $true
    }
    $baselineGeneratedIdentityIds = @{}
    foreach ($item in $baselinePackage.uniqueItems.items)
    {
        if (@($item.sourceObservationIds | Where-Object {
            $baselineGeneratedSourceIds.ContainsKey($_)
        }).Count -gt 0)
        {
            $baselineGeneratedIdentityIds[$item.id] = $true
        }
    }
    $baselineGeneratedBlocks = @($baselinePackage.uniqueItems.items | Where-Object {
        $baselineGeneratedIdentityIds.ContainsKey($_.id)
    } | ForEach-Object { $_.versions } | ForEach-Object { $_.modifierBlocks } | Where-Object {
        $_.kind -eq 'unique'
    })
    $baselineComparison = [ordered]@{
        candidatePath = $baselineCandidate
        candidateSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $baselineCandidate).Hash.ToLowerInvariant()
        mappingCounts = [ordered]@{
            exact = @($baselineBlocks | Where-Object { $_.mechanicalMapping.status -eq 'exact' }).Count
            equivalentSourceSet = @($baselineBlocks | Where-Object { $_.mechanicalMapping.status -eq 'equivalentSourceSet' }).Count
            unsupported = @($baselineBlocks | Where-Object { $_.mechanicalMapping.status -eq 'unsupported' }).Count
            ambiguous = @($baselineBlocks | Where-Object { $_.mechanicalMapping.status -eq 'ambiguous' }).Count
            multiLine = @($baselineBlocks | Where-Object { @($_.lines).Count -gt 1 }).Count
        }
        migrations = [ordered]@{
            uniqueMechanicsNotFoundToResolved = $notFoundMigrations.Count
            uniqueMechanicsNotFoundToExact = @($notFoundMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'exact'
            }).Count
            uniqueMechanicsNotFoundToEquivalentSourceSet = @($notFoundMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'equivalentSourceSet'
            }).Count
            uniqueMechanicsConflictToResolved = $conflictMigrations.Count
            uniqueMechanicsConflictToExact = @($conflictMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'exact'
            }).Count
            uniqueMechanicsConflictToEquivalentSourceSet = @($conflictMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'equivalentSourceSet'
            }).Count
            uniqueMechanicsExactConflictToResolved = $exactConflictMigrations.Count
            uniqueMechanicsExactConflictToExact = @($exactConflictMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'exact'
            }).Count
            uniqueMechanicsExactConflictToEquivalentSourceSet = @($exactConflictMigrations | Where-Object {
                $_.mechanicalMapping.status -eq 'equivalentSourceSet'
            }).Count
            genuinelyUnresolvedAfter = @($uniqueBlocks | Where-Object {
                $_.mechanicalMapping.status -eq 'unsupported' -or
                $_.mechanicalMapping.status -eq 'ambiguous'
            }).Count
            previouslyResolvedNowUnresolved = $resolvedRegressions.Count
            previouslyResolvedNowUnresolvedDetails = @($resolvedRegressions | ForEach-Object {
                $context = $currentContextByBlockId[$_.id]
                [ordered]@{
                    blockId = $_.id
                    canonicalName = $context.canonicalName
                    versionLabel = $context.versionLabel
                    lines = $context.lines
                    beforeStatus = $_.mechanicalMapping.status
                    afterStatus = $currentById[$_.id].mechanicalMapping.status
                    afterDiagnosticCode = $currentById[$_.id].mechanicalMapping.diagnosticCode
                }
            })
            baselineBlocksNotPresentByStableId = @($baselineBlocks | Where-Object {
                -not $currentById.ContainsKey($_.id)
            }).Count
            currentBlocksNotPresentByStableId = @($uniqueBlocks | Where-Object {
                -not $baselineById.ContainsKey($_.id)
            }).Count
        }
        generatedSpecial = [ordered]@{
            before = [ordered]@{
                total = $baselineGeneratedBlocks.Count
                exact = @($baselineGeneratedBlocks | Where-Object { $_.mechanicalMapping.status -eq 'exact' }).Count
                equivalentSourceSet = @($baselineGeneratedBlocks | Where-Object { $_.mechanicalMapping.status -eq 'equivalentSourceSet' }).Count
                unsupported = @($baselineGeneratedBlocks | Where-Object { $_.mechanicalMapping.status -eq 'unsupported' }).Count
                ambiguous = @($baselineGeneratedBlocks | Where-Object { $_.mechanicalMapping.status -eq 'ambiguous' }).Count
            }
            after = [ordered]@{
                total = $generatedUniqueBlocks.Count
                exact = @($generatedUniqueBlocks | Where-Object { $_.mechanicalMapping.status -eq 'exact' }).Count
                equivalentSourceSet = @($generatedUniqueBlocks | Where-Object { $_.mechanicalMapping.status -eq 'equivalentSourceSet' }).Count
                unsupported = @($generatedUniqueBlocks | Where-Object { $_.mechanicalMapping.status -eq 'unsupported' }).Count
                ambiguous = @($generatedUniqueBlocks | Where-Object { $_.mechanicalMapping.status -eq 'ambiguous' }).Count
            }
        }
    }
}

$coverage = [ordered]@{
    generatedAtUtc = [DateTime]::UtcNow.ToString('O')
    stageClassification = 'PartiallyReadyWithExplicitUnsupportedCoverage'
    candidatePath = $candidate
    candidateSha256 = (Get-FileHash -Algorithm SHA256 -LiteralPath $candidate).Hash.ToLowerInvariant()
    schemaVersion = $package.manifest.schemaVersion
    dataVersion = $package.manifest.dataVersion
    totals = [ordered]@{
        sourceObservations = @($package.uniqueItems.sourceObservations).Count
        identities = @($package.uniqueItems.items).Count
        generatedIdentities = @($items | Where-Object generated).Count
        versions = $allVersions.Count
        currentVersions = @($allVersions | Where-Object { $_.role -eq 'current' }).Count
        historicalVersions = @($allVersions | Where-Object { $_.role -eq 'historical' }).Count
        modifierBlocks = $allBlocks.Count
        uniqueModifierBlocks = $uniqueBlocks.Count
        multiLineUniqueBlocks = @($uniqueBlocks | Where-Object { @($_.lines).Count -gt 1 }).Count
        conflicts = $conflicts.Count
    }
    identityCoverage = [ordered]@{
        ordinaryIdentities = @($items | Where-Object { $_.kind -eq 'ordinary' }).Count
        currentCapableIdentities = $currentCapable.Count
        ordinaryCurrentCapableIdentities = $ordinaryCurrentCapable.Count
        historicalCapableIdentities = $historicalCapable.Count
        replicaIdentities = @($items | Where-Object { $_.kind -eq 'replica' }).Count
        foulbornObservedIdentities = @($items | Where-Object { $_.kind -eq 'foulbornObserved' }).Count
        foulbornCapableIdentities = @($package.uniqueItems.foulbornModifierRelationships |
            Where-Object { $_.status -eq 'exact' } |
            Select-Object -ExpandProperty uniqueItemId -Unique).Count
        foulbornCapabilityNote = 'Copied Foulborn identity remains the ordinary underlying identity plus item-scoped, source-proven replacement relationships; official provider eligibility is resolved at runtime.'
        generatedSpecialIdentities = @($items | Where-Object generated).Count
        invalidIdentities = $invalidIdentities.Count
    }
    versionCoverage = [ordered]@{
        total = $allVersions.Count
        current = @($allVersions | Where-Object { $_.role -eq 'current' }).Count
        historical = @($allVersions | Where-Object { $_.role -eq 'historical' }).Count
        identitiesWithBaseChanges = $baseChangeIdentities.Count
        ambiguousButSafeHistoricalGroups = $ambiguousSafeHistoricalGroups.Count
        ambiguousButSafeHistoricalGroupDetails = $ambiguousSafeHistoricalGroups
    }
    modifierBlockCoverage = [ordered]@{
        total = $uniqueBlocks.Count
        exactProviderNeutral = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.status -eq 'exact'
        }).Count
        equivalentSourceSets = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.status -eq 'equivalentSourceSet'
        }).Count
        presenceOnlyCandidates = $presenceOnlyCandidates.Count
        scalarCandidates = $scalarCandidates.Count
        multiLine = @($uniqueBlocks | Where-Object { @($_.lines).Count -gt 1 }).Count
        multiLineOrMultiEffectCandidates = $multiEffectCandidates.Count
        unsupported = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.status -eq 'unsupported'
        }).Count
        unsupportedByDiagnostic = $unsupportedReasonCounts
        ambiguous = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.status -eq 'ambiguous'
        }).Count
        ambiguousByDiagnostic = $ambiguousReasonCounts
        shapeClassificationNote = 'Presence/scalar/multi-effect counts are provider-neutral lexical candidates; official Trade safety is decided at runtime and may still fail closed.'
    }
    semanticFingerprintCoverage = [ordered]@{
        sourceObservations = @($package.uniqueItems.sourceObservations).Count
        totalModifierBlocks = $allBlocks.Count
        blocksWithSourceFingerprint = $fingerprintedBlocks.Count
        sourceLocality = $sourceFingerprintLocalityCounts
        sourceFingerprintKnown = $fingerprintedBlocks.Count - $sourceFingerprintLocalityCounts.unknown
        sourceFingerprintIncomplete = $sourceFingerprintLocalityCounts.unknown
        blocksWithMatchedCandidateFingerprint = $matchedFingerprintBlocks.Count
        comparableMatchedCandidateFingerprints = $comparableMatchedFingerprintBlocks.Count
        incompleteMatchedCandidateFingerprints = $matchedFingerprintBlocks.Count -
            $comparableMatchedFingerprintBlocks.Count
        observationsWithMultipleCandidateSemanticKeys = $multipleSemanticKeyObservationIds.Count
        observationsWithMultipleCandidateSemanticKeyIds = $multipleSemanticKeyObservationIds
        validationFailures = 0
    }
    semanticFamilyAudit = $semanticFamilyAudit
    tradeCoverage = [ordered]@{
        exactModifierMappings = $null
        exactModifierMappingsStatus = 'DeferredToRuntimeOfficialCatalog'
        providerIdentityExact = $null
        providerIdentityFailuresByReason = [ordered]@{
            NOT_EVALUATED_OFFLINE = @($items).Count
        }
        identityReadyInFoundation = @($items).Count - $invalidIdentities.Count
        identityReadyWithUnsupportedModifierBlocks = $identityReadyWithUnsupported.Count
        note = 'Provider IDs are intentionally absent from GameData. Official item/stat catalog matching is exercised at runtime; the staged raw scalar acceptance test proves the end-to-end adapter path.'
    }
    foulbornReplacementEvidence = [ordered]@{
        preserved = @($package.uniqueItems.foulbornModifierRelationships).Count -gt 0
        sourceObservationCount = @($package.uniqueItems.foulbornRelationshipSources).Count
        relationshipCount = @($package.uniqueItems.foulbornModifierRelationships).Count
        exact = @($package.uniqueItems.foulbornModifierRelationships | Where-Object { $_.status -eq 'exact' }).Count
        unsupported = @($package.uniqueItems.foulbornModifierRelationships | Where-Object { $_.status -eq 'unsupported' }).Count
        reason = 'Schema 3 retains item-scoped normal-to-Foulborn modifier relationships from the pinned PoB generated map. Runtime resolution remains fail-closed.'
    }
    identityKinds = $kindCounts
    identityClassifications = $classificationCounts
    mechanicalMappings = $mappingCounts
    baselineComparison = $baselineComparison
    remainingRootCauseFamilies = [ordered]@{
        generatedMechanicsNotFound = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.diagnosticCode -eq 'UNIQUE_GENERATED_MECHANICS_NOT_FOUND'
        }).Count
        exactEvidenceConflict = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_EXACT_CONFLICT'
        }).Count
        normalizedSignatureConflict = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_CONFLICT'
        }).Count
        noTranslatedMechanicsObservation = @($uniqueBlocks | Where-Object {
            $_.mechanicalMapping.diagnosticCode -eq 'UNIQUE_MECHANICS_NOT_FOUND'
        }).Count
    }
    items = $items
}

$pobSource = @($package.manifest.sources | Where-Object { $_.sourceId -eq 'path-of-building' })
$pobMetadata = [ordered]@{
    source = if ($pobSource.Count -eq 1) { $pobSource[0] } else { $null }
    observedRepositoryUris = @($package.uniqueItems.sourceObservations.repositoryUri | Sort-Object -Unique)
    observedTags = @($package.uniqueItems.sourceObservations.tag | Sort-Object -Unique)
    observedCommits = @($package.uniqueItems.sourceObservations.commitSha | Sort-Object -Unique)
    observedPaths = @($package.uniqueItems.sourceObservations.sourcePath | Sort-Object -Unique)
    normalObservationCount = @($package.uniqueItems.sourceObservations | Where-Object { -not $_.isGenerated }).Count
    generatedObservationCount = @($package.uniqueItems.sourceObservations | Where-Object isGenerated).Count
}

$candidateMetadata = [ordered]@{
    path = $candidate
    sizeBytes = (Get-Item -LiteralPath $candidate).Length
    sha256 = $coverage.candidateSha256
    schemaVersion = $coverage.schemaVersion
    dataVersion = $coverage.dataVersion
    manifestSources = $package.manifest.sources
    counts = $coverage.totals
    activated = $false
}

$utf8 = [System.Text.UTF8Encoding]::new($false)
function Write-Json([string]$Name, $Value)
{
    [System.IO.File]::WriteAllText(
        (Join-Path $outputRoot $Name),
        ($Value | ConvertTo-Json -Depth 20),
        $utf8)
}
Write-Json 'coverage.json' $coverage
Write-Json 'candidate-metadata.json' $candidateMetadata
Write-Json 'pob-source-metadata.json' $pobMetadata
Write-Json 'unique-conflicts.json' ([ordered]@{ count = $conflicts.Count; conflicts = $conflicts })

$stageDeltaMarkdown = if ($null -eq $coverage.baselineComparison)
{
    '- Baseline comparison: **not requested**'
}
else
{
    @"
- ``UNIQUE_MECHANICS_NOT_FOUND`` to Exact/Equivalent: **$($coverage.baselineComparison.migrations.uniqueMechanicsNotFoundToResolved)**
- ``UNIQUE_MECHANICS_CONFLICT`` to Exact/Equivalent: **$($coverage.baselineComparison.migrations.uniqueMechanicsConflictToResolved)**
- Genuinely unresolved after: **$($coverage.baselineComparison.migrations.genuinelyUnresolvedAfter)**
- Generated/special resolved before: **$($coverage.baselineComparison.generatedSpecial.before.exact + $coverage.baselineComparison.generatedSpecial.before.equivalentSourceSet)** / $($coverage.baselineComparison.generatedSpecial.before.total)
- Generated/special resolved after: **$($coverage.baselineComparison.generatedSpecial.after.exact + $coverage.baselineComparison.generatedSpecial.after.equivalentSourceSet)** / $($coverage.baselineComparison.generatedSpecial.after.total)
"@
}

$markdown = @"
# PoEnhance Unique catalog coverage

- Candidate: ``$candidate``
- Candidate SHA-256: ``$($coverage.candidateSha256)``
- Schema/data version: ``$($coverage.schemaVersion)`` / ``$($coverage.dataVersion)``
- Source observations: **$($coverage.totals.sourceObservations)**
- Unique identities: **$($coverage.totals.identities)** ($($coverage.totals.generatedIdentities) generated)
- Versions: **$($coverage.totals.versions)** ($($coverage.totals.currentVersions) current; $($coverage.totals.historicalVersions) historical)
- Unique modifier blocks: **$($coverage.totals.uniqueModifierBlocks)**
- Multi-line Unique modifier blocks: **$($coverage.totals.multiLineUniqueBlocks)**
- Mechanical conflicts: **$($coverage.totals.conflicts)**
- Stage classification: **$($coverage.stageClassification)**

## Identity coverage

- Ordinary/current-capable: **$($coverage.identityCoverage.ordinaryCurrentCapableIdentities)**
- Historical-capable: **$($coverage.identityCoverage.historicalCapableIdentities)**
- Replica: **$($coverage.identityCoverage.replicaIdentities)**
- Foulborn observations: **$($coverage.identityCoverage.foulbornObservedIdentities)**
- Generated/special: **$($coverage.identityCoverage.generatedSpecialIdentities)**
- Invalid identities: **$($coverage.identityCoverage.invalidIdentities)**

## Version coverage

- Base changes across versions: **$($coverage.versionCoverage.identitiesWithBaseChanges)**
- Ambiguous-but-safe historical groups: **$($coverage.versionCoverage.ambiguousButSafeHistoricalGroups)**

## Modifier block coverage

- Exact provider-neutral: **$($coverage.modifierBlockCoverage.exactProviderNeutral)**
- Equivalent source sets: **$($coverage.modifierBlockCoverage.equivalentSourceSets)**
- Presence-only candidates: **$($coverage.modifierBlockCoverage.presenceOnlyCandidates)**
- Scalar candidates: **$($coverage.modifierBlockCoverage.scalarCandidates)**
- Multi-line/multi-effect candidates: **$($coverage.modifierBlockCoverage.multiLineOrMultiEffectCandidates)**
- Unsupported: **$($coverage.modifierBlockCoverage.unsupported)**
- Ambiguous: **$($coverage.modifierBlockCoverage.ambiguous)**

## Stage delta

$stageDeltaMarkdown

Official Trade identity/stat coverage is deliberately deferred to runtime catalog matching; provider IDs are not stored in this package. Foundation identity ready with Unsupported blocks: **$($coverage.tradeCoverage.identityReadyWithUnsupportedModifierBlocks)**.

## Identity classifications

``````json
$($classificationCounts | ConvertTo-Json)
``````

## Mechanical mapping outcomes

``````json
$($mappingCounts | ConvertTo-Json)
``````
"@
[System.IO.File]::WriteAllText((Join-Path $outputRoot 'coverage.md'), $markdown, $utf8)

Write-Host "Coverage JSON: $(Join-Path $outputRoot 'coverage.json')"
Write-Host "Coverage Markdown: $(Join-Path $outputRoot 'coverage.md')"
