# Update Server - Server Sync

This repository provides tools for synchronizing Microsoft Update catalogs, combining **metadata stores** and **content stores** to manage update metadata and actual content files.

---

## Overview

- **Metadata Store:** Keeps track of update information, descriptions, relationships, rules, KB articles, and other properties. Indexing and query is highly optimized.
- **Content Store:** Manages the actual payload files referenced by updates—such as patch binaries or cabinet files.

---

## Core Visual Architecture

### High-Level Data Flow

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

## 1. Lifecycle Diagram

```mermaid
stateDiagram-v2
    [*] --> FetchMetadata
    FetchMetadata --> ParseMetadata
    ParseMetadata --> DownloadContent
    DownloadContent --> StoreContent
    StoreContent --> ServeToClient
    ServeToClient --> [*]
```
*Shows the stages an update passes through: fetching metadata, parsing, downloading content, storing, serving.*

---

## 2. Error Handling and Retry Flow

```mermaid
flowchart TD
    TryDownload -->|Success| StoreContent
    TryDownload -->|Error| Retry[Retry]
    Retry -->|MaxRetries| Fail[FailureNotify]
    Retry -->|Success| StoreContent
```
*Visualizes the retry/failure approach taken by content store operations.*

---

## 3. Component Diagram

```mermaid
classDiagram
    IMetadataStore <|.. DirectoryMetadataStore
    IMetadataStore <|.. CompressedMetadataStore
    IMetadataStore <|.. AzureBlobMetadataStore
    IContentStore <|.. FileSystemContentStore
    IContentStore <|.. BlobContentStore
```
*Outlines interface inheritance and extensibility for metadata/content stores.*

---

## 4. Sequence Diagram

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
*Illustrates update fetch and content download operation.*

---

## 5. Storage Layout Examples

#### Metadata Store

```
store/
└── metadata/
    └── partitions/
        └── [partition]/
            └── [index]/
                └── [updateId].xml
```

#### Content Store

```
content/
└── [updateId]/
    └── [actual file]
```
*Clarifies on-disk structure for local storage.*

---

## 6. Synchronization Scenario Diagram

```mermaid
graph TB
  Upstream["Microsoft Update"] --> LocalServer["Your Server"]
  LocalServer --> ClientA
  LocalServer --> ClientB
  LocalServer --> ClientC
```
*Demonstrates typical multi-client sync architecture.*

---

## 7. API Usage Diagram

```mermaid
graph TD
    user[User] --> PackageStore
    PackageStore --> MetadataStore
    PackageStore --> ContentStore
    user --> ContentStore
```
*Shows API entry points and key classes.*

---

## 8. Concurrency Diagram

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
*Visualizes thread safety and locking for concurrent operations.*

---

## 9. Extensibility Layer

```mermaid
classDiagram
    IMetadataStore <|.. DirectoryMetadataStore
    IMetadataStore <|.. AzureBlobMetadataStore
    IMetadataStore <|.. CustomProvider
    IContentStore <|.. FileSystemContentStore
    IContentStore <|.. BlobContentStore
    IContentStore <|.. CustomContentProvider
```
*Illustrates how to add custom storage providers.*

---

## 10. Update Object Model Relationships

```mermaid
graph LR
    KB1234 --- Update1
    Update1 -- supersedes --> Update2
    Update2 --- KB9999
    Update1 -- requires --> Update3
```
*Shows how updates relate through metadata—supersedence, KB, requirements.*

---

## Decision Table

| Use Case                | Metadata Store Backend      | Content Store Backend   | Pros                  | Cons                      |
|-------------------------|----------------------------|------------------------|-----------------------|---------------------------|
| Small/local deployment  | DirectoryMetadataStore     | FileSystemContentStore | Easy, fast filesystem | Not cloud-scalable        |
| Cloud/distributed       | AzureBlobMetadataStore     | BlobContentStore       | Cloud-based, scalable | Needs setup, network cost |
| Compressed archive      | CompressedMetadataStore    | FileSystemContentStore | One file, portable    | Slower queries            |

---

## Further Reading

- [IMetadataStore API documentation](https://github.com/Sntai20/update-server-server-sync/blob/main/docs/api/Microsoft.PackageGraph.Storage.IMetadataStore.html)
- [IContentStore API documentation](https://github.com/Sntai20/update-server-server-sync/blob/main/docs/api/Microsoft.PackageGraph.Storage.IContentStore.html)
- [Example: fetching and storing content](https://github.com/Sntai20/update-server-server-sync/blob/main/src/documentation/docfx-config/examples/content-store.md)
- Source files:
  - [MetadataStore (Azure)](https://github.com/Sntai20/update-server-server-sync/blob/main/src/microsoft-update-partition/Storage/AzureBlob/MetadataStore.cs)
  - [DirectoryMetadataStore (Filesystem)](https://github.com/Sntai20/update-server-server-sync/blob/main/src/microsoft-update-partition/Storage/FileSystem/DirectoryMetadataStore.cs)
  - [FileSystemContentStore](https://github.com/Sntai20/update-server-server-sync/blob/main/src/microsoft-update-partition/Storage/FileSystem/FileSystemContentStore.cs)
  - [BlobContentStore (Azure)](https://github.com/Sntai20/update-server-server-sync/blob/main/src/microsoft-update-partition/Storage/AzureBlob/BlobContentStore.cs)

---

## License

MIT
