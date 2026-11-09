# Data Model: Readable Log Directories

This feature adds no database entity or migration. The model below describes runtime and filesystem
state used to select one legacy project log directory.

## Run Start Instant

Represents the single UTC instant captured for a configured TestMap run.

| Field | Type | Rules |
|---|---|---|
| `started_at_utc` | timestamp with offset | Required; offset must be UTC; immutable for the run |
| `date_segment` | string | Derived as `yyyy-MM-dd` from `started_at_utc` |
| `time_segment` | string | Derived as `HH-mm-ss` from `started_at_utc` |

**Relationships**: One run start instant may be shared by all project models initialized by one
configuration service. Directly constructed project models own one equivalent captured instant.

## Legacy Log Directory Candidate

Represents one possible final directory for a project run.

| Field | Type | Rules |
|---|---|---|
| `log_root` | path | Existing configured root behavior applies |
| `date_segment` | string | `yyyy-MM-dd` |
| `owner` | string | Existing normalized repository owner label |
| `repository` | string | Existing normalized repository name label |
| `ordinal` | integer | 1 for unsuffixed base; 2 or greater for `-NN` |
| `directory_leaf` | string | `HH-mm-ss_owner-repository` plus optional ordinal |
| `directory_path` | path | Must remain beneath `log_root/date_segment` |

**Validation rules**:

- Ordinal 1 has no suffix.
- Ordinal 2 and above use at least two zero-padded digits (`-02`, `-03`, ...).
- Final directory names contain no random or GUID component.
- An existing candidate is occupied and cannot be reused.

## Log Directory Reservation

Represents ownership of one candidate by one project run.

| Field | Type | Rules |
|---|---|---|
| `candidate` | Legacy Log Directory Candidate | Required |
| `marker_path` | path | Fixed basename `.testmap-log-reservation` inside candidate |
| `reserved` | boolean | True only after exclusive marker creation succeeds |
| `log_file_path` | path | Existing `<ProjectId>.log` within the reserved candidate |

**State transitions**:

```text
Candidate
  -> Occupied (directory already exists; advance ordinal)
  -> DirectoryCreated
       -> LostRace (exclusive marker exists; advance ordinal)
       -> Reserved (exclusive marker created)
            -> LoggerReady
            -> PartialFailure (reservation remains visible)
```

`Occupied`, `LostRace`, and `PartialFailure` are never converted into reusable success for another
run.

## Project Log Selection

Runtime state retained by one project model after successful initialization.

| Field | Type | Rules |
|---|---|---|
| `run_started_at_utc` | timestamp with offset | Captured once |
| `selected_directory_path` | path, optional | Set once for legacy logging |
| `logs_file_path` | path, optional | Set once and exposed through existing evidence surfaces |
| `project_id` | string | Existing identity policy; unchanged by this feature |
| `materialized_revision` | reference, optional | When present, its log path updates to the selected `run.log`; non-log revision paths remain unchanged |

Repeated initialization after `logs_file_path` is selected is idempotent and does not allocate a new
ordinal.
