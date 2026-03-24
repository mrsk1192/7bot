# Resource Query Protocol

## Input

- `GET /api/query_resource_candidates`
- `POST /api/command` with `Command: "query_resource_candidates"`

Supported parameters:

- `x`, `y`, `z` or `center`
- `radius`
- `max_results`
- `include_surface_only`
- `candidate_categories`
- `likely_resource_types`
- `min_confidence`
- `sort_by`
- `include_exposed_only`

## Output

- `Center`
- `Radius`
- `MaxResults`
- `SortBy`
- `Count`
- `IgnoredFilters`
- `Candidates[]`
  - `Name`
  - `BlockId`
  - `Position`
  - `Distance`
  - `CandidateCategory`
  - `CandidateConfidence`
  - `LikelyResourceType`
  - `IsExposed`
  - `Biome`
  - `LineOfSightClear`
  - `ReachableHint`
  - `Note`

## Rules

- Candidates are derived from nearby block inspection, not from CV/OCR.
- The API never performs global exploration. It is radius-limited and `max_results` limited.
- Unsupported filters are ignored safely and echoed in `IgnoredFilters` when needed.
- Sorting supports `distance`, `confidence`, and `type`.

## Unknown Conditions

- `LineOfSightClear` and `ReachableHint` can return `unknown` if the necessary player/world data is missing.
- `LikelyResourceType` can remain `unknown` for generic ore/resource names that do not map to a specific material.

## Known Limitations

- Resource classification is heuristic and block-name driven for many blocks.
- Deep underground candidates are returned only if they fall within the requested radius volume.
