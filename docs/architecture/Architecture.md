# Update Server Architectural Overview

This document details architectural concepts, workflows, and visualizations for the `update-server-server-sync` repository. It aims to assist contributors, maintainers, and users in understanding how the update pipeline works and how the system remains available during updates.

---

## Table of Contents

- [High-Level Data Flow](#high-level-data-flow)
- [Lifecycle Diagram](#lifecycle-diagram)
- [Error Handling and Retry Flow](#error-handling-and-retry-flow)
- [Component Diagram](#component-diagram)
- [Sequence Diagram](#sequence-diagram)
- [Storage Layouts](#storage-layouts)
- [Synchronization Scenarios](#synchronization-scenarios)
- [API Usage](#api-usage)
- [Concurrency & Locking](#concurrency--locking)
- [Extensibility Layer](#extensibility-layer)
- [Update Object Relationships](#update-object-relationships)
- [Zero-Downtime Updating](#zero-downtime-updating)
- [Decision Table](#decision-table)

---

## High-Level Data Flow

```mermaid
flowchart LR
    subgraph Upstream
      PackageGraph((Microsoft Update PackageGraph))
    end

    subgraph "Local Server"
      MS[Metadata Store]
      CS[Content Store]
    end

    PackageGraph-- Sync Metadata --> MS
    MS -- Query for Update --> CS
    CS -- Serve Content Files --> Client[Downstream Clients]
```

---

## Lifecycle Diagram

Shows the lifecycle of an update from remote sync to client serving.

```mermaid
stateDiagram-v2
    [*] --> FetchMetadata
    FetchMetadata --> ParseMetadata
    ParseMetadata --> DownloadContent
    DownloadContent --> StoreContent
    StoreContent --> ServeToClient
    ServeToClient --> [*]
```

---

## Error Handling and Retry Flow

Visualizes error management and retries during download/update operations.

```mermaid
flowchart TD
    TryDownload -->|Success| StoreContent
    TryDownload -->|Error| Retry[Retry]
    Retry -->|MaxRetries| Fail[FailureNotify]
    Retry -->|Success| StoreContent
```

---

## Component Diagram

Displays the core interfaces and implementations.

```mermaid
classDiagram
    IMetadataStore <|.. DirectoryMetadataStore
    IMetadataStore <|.. CompressedMetadataStore
    IMetadataStore <|.. AzureBlobMetadataStore
    IContentStore <|.. FileSystemContentStore
    IContentStore <|.. BlobContentStore
```

---

## Sequence Diagram

Typical client interaction for update query and content download.

```mermaid
sequenceDiagram
    participant User
    participant MetadataStore
    participant ContentStore

    User->>MetadataStore: Query for update metadata
    MetadataStore-->>User: Return update list
    User->>ContentStore: Request file(s) for update
    ContentStore-->>User: Provide update file(s)
```

---

## Storage Layouts

### Metadata Store

```
store/
└── metadata/
    └── partitions/
        └── [partition]/
            └── [index]/
                └── [updateId].xml
```

### Content Store

```
content/
└── [updateId]/
    └── [actual file]
```

---

## Synchronization Scenarios

Demonstrates upstream sync and multi-client serving.

```mermaid
graph TB
  Upstream["Microsoft Update"] --> LocalServer["Your Server"]
  LocalServer --> ClientA
  LocalServer --> ClientB
  LocalServer --> ClientC
```

---

## API Usage

Primary classes and entry points for common user operations.

```mermaid
graph TD
    user[User] --> PackageStore
    PackageStore --> MetadataStore
    PackageStore --> ContentStore
    user --> ContentStore
```

---

## Concurrency & Locking

Critical for zero-downtime updates, thread safety is achieved via locks and atomic swaps.

```mermaid
sequenceDiagram
    participant Thread1
    participant Thread2
    participant MetadataStore

    Thread1->>MetadataStore: lock MetadataBlobLock
    Thread2->>MetadataStore: wait for lock
    MetadataStore-->>Thread1: Process
    MetadataStore-->>Thread2: lock available
```

---

## Extensibility Layer

Supports adding new metadata/content store providers.

```mermaid
classDiagram
    IMetadataStore <|.. DirectoryMetadataStore
    IMetadataStore <|.. AzureBlobMetadataStore
    IMetadataStore <|.. CustomProvider
    IContentStore <|.. FileSystemContentStore
    IContentStore <|.. BlobContentStore
    IContentStore <|.. CustomContentProvider
```

---

## Update Object Relationships

How updates, KBs, and requirements interact.

```mermaid
graph LR
    KB1234 --- Update1
    Update1 -- supersedes --> Update2
    Update2 --- KB9999
    Update1 -- requires --> Update3
```

---

## Zero-Downtime Updating

**How metadata and content are updated while serving clients:**

- **Background sync:** Run fetch/update jobs periodically in background threads or processes.
- **Staging areas:** Sync new metadata/content to temporary storage, not affecting current service.
- **Atomic swaps:** Swap files/indices with new versions only after sync completes.
- **Locking/concurrency:** `ReaderWriterLockSlim` or `object locks` (see repo code) prevent serving partial/inconsistent data.
- **Versioning:** Retain prior content or metadata while client requests are in-flight.
- **Indexation:** Reindex new metadata/content in the background, swapping only after health checks.
- **Failover:** If update fails, old versions are retained for uninterrupted client service.

### Fast Update Sequence

```mermaid
sequenceDiagram
    participant Client
    participant Server
    participant SyncJob

    Client->>Server: Request metadata/content (served from active version)
    SyncJob->>Server: Background fetch and stage new data
    SyncJob->>Server: Acquire lock, swap in new metadata/content
    Server-->>Client: Next request, serve new version
```

---

## Decision Table

| Use Case                | Metadata Store Backend      | Content Store Backend   | Pros                  | Cons                      |
|-------------------------|----------------------------|------------------------|-----------------------|---------------------------|
| Small/local deployment  | DirectoryMetadataStore     | FileSystemContentStore | Easy, fast filesystem | Not cloud-scalable        |
| Cloud/distributed       | AzureBlobMetadataStore     | BlobContentStore       | Cloud-based, scalable | Needs setup, network cost |
| Compressed archive      | CompressedMetadataStore    | FileSystemContentStore | One file, portable    | Slower queries            |

---

## References & Further Reading

- [IMetadataStore API documentation](docs/api/Microsoft.PackageGraph.Storage.IMetadataStore.html)
- [IContentStore API documentation](docs/api/Microsoft.PackageGraph.Storage.IContentStore.html)
- [Example: fetching and storing content](src/documentation/docfx-config/examples/content-store.md)
- Source files:
  - [MetadataStore (Azure)](src/microsoft-update-partition/Storage/AzureBlob/MetadataStore.cs)
  - [DirectoryMetadataStore (Filesystem)](src/microsoft-update-partition/Storage/FileSystem/DirectoryMetadataStore.cs)
  - [FileSystemContentStore](src/microsoft-update-partition/Storage/FileSystem/FileSystemContentStore.cs)
  - [BlobContentStore (Azure)](src/microsoft-update-partition/Storage/AzureBlob/BlobContentStore.cs)

---

## License

MIT
