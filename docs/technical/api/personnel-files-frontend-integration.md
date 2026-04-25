# PersonnelFiles Frontend Integration Guide

## Purpose

This document summarizes the API changes that frontend must adopt for the `PersonnelFiles` module after the section-based refactor and document storage migration to Azure Blob Storage.

Canonical technical references remain:

- `docs/technical/api/endpoint-reference.md`
- `docs/technical/api/openapi.yaml`

This guide is intentionally frontend-oriented and focuses on request/response behavior and integration impact.

## High-Level Changes

### 1. `GET /api/v1/personnel-files/{id}` is now lightweight

This endpoint no longer returns the full personnel file with all sections embedded.

Frontend must treat it as a shell/bootstrap endpoint only.

Use it to obtain:

- personnel file identity and basic header data
- lifecycle state
- organization linkage
- current `concurrencyToken`
- high-level actions or status needed to initialize the screen

Do **not** expect this endpoint to return:

- addresses
- documents
- bank accounts / payment methods
- employee relations
- evaluations
- family members
- any other tab/section collections

### 2. Section tabs must load their own data

Each UI tab or section must call its dedicated endpoint only when needed.

Examples:

- `GET /api/v1/personnel-files/{id}/personal-info`
- `GET /api/v1/personnel-files/{id}/addresses`
- `GET /api/v1/personnel-files/{id}/documents`
- `GET /api/v1/personnel-files/{id}/payment-methods`
- `GET /api/v1/personnel-files/{id}/employee-relations`
- `GET /api/v1/personnel-files/{id}/observations`

This is now the expected loading pattern for performance reasons.

### 3. Section writes are section-scoped

Frontend should update each section through its own endpoint instead of expecting one full-file update flow.

Typical pattern:

- `GET` section when opening the tab
- `POST` when creating a new item inside that section
- `PUT` when replacing/updating the section or item, depending on the endpoint contract
- `PATCH` when the endpoint is explicitly partial by design

## Expected Frontend Loading Pattern

Recommended screen flow:

1. Call `GET /api/v1/personnel-files/{id}` when entering the personnel file detail screen.
2. Render the header/shell.
3. Lazy-load each section only when the user opens that tab.
4. After a successful write, update local state from the response payload instead of reloading the entire personnel file.

Avoid this old pattern:

1. Call one full `GET /personnel-files/{id}`.
2. Hydrate the whole page from a single oversized payload.

That pattern is no longer valid.

## `GET`, `POST`, `PUT` Behavior by Section

## `GET`

Use `GET` endpoints for section reads only.

Frontend rule:

- each section should assume it owns its own fetch lifecycle
- do not depend on another section response containing its data

Examples:

- `GET /api/v1/personnel-files/{id}/documents`
- `GET /api/v1/personnel-files/{id}/addresses`
- `GET /api/v1/personnel-files/{id}/salary-items`

## `POST`

Use `POST` when the endpoint creates a new child resource or new entry in a section.

Examples already used in `PersonnelFiles`:

- `POST /api/v1/personnel-files/{id}/documents`
- `POST /api/v1/personnel-files/{id}/observations`

Frontend rule:

- treat `POST` responses as the source of truth for the created item
- if the response includes a new concurrency token, replace the local one immediately

## `PUT`

Use `PUT` when the section contract is replace/update-oriented.

Examples:

- `PUT /api/v1/personnel-files/{id}/addresses`
- `PUT /api/v1/personnel-files/{id}/personal-info`
- `PUT /api/v1/personnel-files/{id}/payment-methods`

Frontend rule:

- send the section payload expected by that endpoint
- include the section or personnel file concurrency token when required by the contract
- update the local section state from the response instead of forcing a full-detail reload

## Document Module Changes

## Before

The old document flow assumed:

- upload through backend
- binary content stored in database
- separate download endpoint to retrieve the file

## Now

Documents are now stored in Azure Blob Storage.

Database stores:

- `blobUrl` canonical reference
- `blobName` internal storage identifier
- file metadata

Frontend receives:

- `fileUrl`

Important:

- `fileUrl` is the value frontend must use to open/download the file
- frontend should not construct blob URLs manually
- frontend should not expect a dedicated backend `/download` endpoint anymore

## New Document Read Contract

`GET /api/v1/personnel-files/{id}/documents`

Now returns document metadata including `fileUrl`.

Expected usage:

- render file name, type, size, dates, observations
- use `fileUrl` for preview, open or direct download actions

Do not call a backend document download endpoint.

Document responses already provide `fileUrl`, and there is no dedicated `/api/v1/personnel-file-documents/{documentId}/download` route.

## New Document Upload Contract

`POST /api/v1/personnel-files/{id}/documents`

Frontend still uploads the file using `multipart/form-data`.

Expected result:

- created document metadata
- resolved `fileUrl`
- updated concurrency token where applicable

Frontend should update the documents list from the response instead of assuming a later download call is required.

## New File Replacement Contract

### Endpoint

`PATCH /api/v1/personnel-file-documents/{documentId}/file`

### Purpose

Replace only the binary file of an existing document.

This endpoint is for:

- replacing the uploaded file content
- changing file name/content type/size/hash

This endpoint is **not** for:

- editing document metadata fields like observations or dates
- inactivating the document

## Request Format

Content type:

- `multipart/form-data`

Fields:

- `concurrencyToken`
- `file`

Example:

```http
PATCH /api/v1/personnel-file-documents/{documentId}/file
Content-Type: multipart/form-data
```

Form data:

```text
concurrencyToken = 6ec0ef84-4ce4-4f46-95a6-a877eb8d67f0
file = <binary file>
```

## Frontend Rules for File Replacement

1. Read the current document item from `GET /documents` or from the last document response.
2. Take the current document `concurrencyToken`.
3. Build a `FormData`.
4. Append the new file under `file`.
5. Append the current `concurrencyToken`.
6. Send `PATCH /api/v1/personnel-file-documents/{documentId}/file`.
7. Replace the local document item with the response payload.

Do not:

- send JSON for this endpoint
- reuse an old concurrency token after a successful replacement
- call a separate download endpoint after replacement

The response already contains the new metadata and the new `fileUrl`.

## Suggested Frontend Example

```ts
async function replacePersonnelFileDocumentFile(
  documentId: string,
  concurrencyToken: string,
  file: File
) {
  const formData = new FormData();
  formData.append("concurrencyToken", concurrencyToken);
  formData.append("file", file);

  const response = await fetch(
    `/api/v1/personnel-file-documents/${documentId}/file`,
    {
      method: "PATCH",
      body: formData,
      credentials: "include",
    }
  );

  if (!response.ok) {
    throw new Error("Failed to replace personnel file document.");
  }

  return response.json();
}
```

## UI/UX Impact for Documents

Frontend should now support these actions:

- upload document
- list documents
- open/download document via `fileUrl`
- replace file via `PATCH /api/v1/personnel-file-documents/{documentId}/file`
- inactivate document

Recommended behavior after upload or replacement:

- immediately refresh the document row/card from the response
- update local `concurrencyToken`
- replace the previous `fileUrl` with the new one

## Error Handling Notes

Frontend should expect standard API validation/business errors for:

- invalid concurrency token
- unsupported file type
- file too large
- document not found
- storage not configured

If a request fails because of concurrency:

- reload the current document list or section
- show the user that the document changed and the action must be retried with fresh state

## Frontend Migration Checklist

- Stop using full-detail `GET /api/v1/personnel-files/{id}` as the source for all tabs.
- Lazy-load section endpoints by tab.
- Do not expect `/download` endpoints for personnel file documents.
- Use `fileUrl` from document responses for open/download.
- Add support for `PATCH /api/v1/personnel-file-documents/{documentId}/file`.
- Send document file replacements as `multipart/form-data`.
- Refresh local concurrency tokens after every successful document mutation.

## Final Recommendation

For `PersonnelFiles`, frontend should move to a section-driven state model:

- one lightweight shell request for the screen
- one request per section when the user actually opens it
- one mutation per section/resource
- local UI refresh from mutation response payloads

That pattern is now aligned with backend performance and contract direction.
