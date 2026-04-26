# PersonnelFiles Frontend Integration Guide

## Purpose

This document is the frontend-facing guide for the `PersonnelFiles` document module after the document update contract unification.

Use this guide when implementing or adjusting:

- document tab reads
- document creation
- document metadata edits
- document file replacement
- document removal from the active UI list

Canonical technical references remain:

- `docs/technical/api/openapi.yaml`
- `docs/technical/api/endpoint-reference.md`

This file is intentionally integration-oriented and focuses on what frontend must do.

## Executive Summary

Frontend must now treat the documents section as a synchronized collection.

The canonical write path is:

- `PUT /api/v1/personnel-files/{personnelFileId}/documents`

This single request is now responsible for:

- creating new documents
- updating metadata of existing documents
- replacing files of existing documents
- inactivating documents omitted from the submitted collection

The old per-document update flow no longer applies.

Removed write routes:

- `PATCH /api/v1/personnel-file-documents/{publicId}/file`
- `PATCH /api/v1/personnel-file-documents/{publicId}/inactivate`
- any legacy hybrid route under `/api/v1/personnel-files/documents/{publicId}/...`

## Current Contract

### Read documents

- `GET /api/v1/personnel-files/{personnelFileId}/documents`

Returns a lightweight collection of `PersonnelFileDocumentMetadataResponse`.

Frontend should use this endpoint to:

- render the current documents list
- resolve the current `publicId` of each document
- open or download files using `fileUrl`
- keep local state aligned after reloads

Important:

- `fileUrl` is the read URL the UI must use
- frontend must not construct blob URLs manually
- frontend must not expect a backend `/download` endpoint

### Create one document

- `POST /api/v1/personnel-files/{personnelFileId}/documents`

This route still exists and can still create a single document.

Recommended frontend usage:

- use `PUT /documents` as the default edit/sync flow for the tab
- reserve `POST /documents` only if the UX intentionally supports isolated single-document creation before a full section sync

### Sync the full documents section

- `PUT /api/v1/personnel-files/{personnelFileId}/documents`

Content type:

- `multipart/form-data`

Required form fields:

- `concurrencyToken`
- `manifestJson`

Additional form parts:

- one file part per manifest item that includes `fileKey`

## Request Shape

### `manifestJson`

`manifestJson` must serialize to:

```json
{
  "items": [
    {
      "documentPublicId": "4b0204c2-8688-4fe3-bf8b-a5351f4ff2c2",
      "documentType": "CONSTANCIA",
      "observations": "Solo actualiza metadatos",
      "deliveryDate": "2026-04-01T00:00:00Z",
      "loanDate": null,
      "returnDate": null,
      "fileKey": null
    }
  ]
}
```

Each item supports:

- `documentPublicId?: string`
- `documentType: string`
- `observations?: string | null`
- `deliveryDate?: string | null`
- `loanDate?: string | null`
- `returnDate?: string | null`
- `fileKey?: string | null`

## Backend Resolution Rules

Frontend should build payloads using these exact rules:

1. `documentPublicId` absent + `fileKey` present
Creates a new document.

2. `documentPublicId` present + `fileKey` absent
Updates metadata only. The existing blob remains unchanged.

3. `documentPublicId` present + `fileKey` present
Updates metadata and replaces the file in the same request.

4. `documentPublicId` absent + `fileKey` absent
Invalid request.

5. Existing active document omitted from `items`
The backend inactivates it.

Important implication:

- `PUT /documents` is a full sync of the active collection, not a partial patch.

## How Frontend Should Model the Tab

Recommended UI mental model:

- the tab edits an in-memory array of active documents
- each row may represent either an existing persisted document or a new unsaved document
- on save, frontend serializes the full active array into `manifestJson`
- only rows with a newly selected file should include `fileKey`

Recommended per-row state:

```ts
type EditableDocumentRow = {
  documentPublicId?: string;
  documentType: string;
  observations?: string | null;
  deliveryDate?: string | null;
  loanDate?: string | null;
  returnDate?: string | null;
  selectedFile?: File | null;
  removeFromActiveList?: boolean;
};
```

Before sending:

- exclude rows marked for removal from `items`
- generate one unique `fileKey` per row that has `selectedFile`
- append only those files to `FormData`

## Example Request

Example scenario:

- first item already exists and only changes metadata
- second item already exists and replaces file
- third item is new
- any other previously active document not sent will become inactive

```http
PUT /api/v1/personnel-files/{personnelFileId}/documents
Content-Type: multipart/form-data
```

```text
concurrencyToken = 6ec0ef84-4ce4-4f46-95a6-a877eb8d67f0
manifestJson = {
  "items": [
    {
      "documentPublicId": "4b0204c2-8688-4fe3-bf8b-a5351f4ff2c2",
      "documentType": "CONSTANCIA",
      "observations": "Actualiza solo metadatos"
    },
    {
      "documentPublicId": "d621a927-d306-4c36-8d91-228fa06ece43",
      "documentType": "EXPEDIENTE",
      "observations": "Reemplazo de archivo",
      "fileKey": "file-1"
    },
    {
      "documentType": "DIPLOMA",
      "observations": "Documento nuevo",
      "fileKey": "file-2"
    }
  ]
}
file-1 = <binary>
file-2 = <binary>
```

## Suggested Frontend Helper

```ts
type PutDocumentItem = {
  documentPublicId?: string;
  documentType: string;
  observations?: string | null;
  deliveryDate?: string | null;
  loanDate?: string | null;
  returnDate?: string | null;
  fileKey?: string;
};

function buildDocumentsFormData(input: {
  concurrencyToken: string;
  rows: EditableDocumentRow[];
}) {
  const formData = new FormData();
  const items: PutDocumentItem[] = [];

  formData.append("concurrencyToken", input.concurrencyToken);

  let fileIndex = 0;

  for (const row of input.rows) {
    if (row.removeFromActiveList) {
      continue;
    }

    const item: PutDocumentItem = {
      documentPublicId: row.documentPublicId,
      documentType: row.documentType,
      observations: row.observations ?? null,
      deliveryDate: row.deliveryDate ?? null,
      loanDate: row.loanDate ?? null,
      returnDate: row.returnDate ?? null
    };

    if (row.selectedFile) {
      const fileKey = `file-${fileIndex++}`;
      item.fileKey = fileKey;
      formData.append(fileKey, row.selectedFile);
    }

    items.push(item);
  }

  formData.append("manifestJson", JSON.stringify({ items }));

  return formData;
}
```

Example call:

```ts
async function saveDocuments(personnelFileId: string, concurrencyToken: string, rows: EditableDocumentRow[]) {
  const formData = buildDocumentsFormData({ concurrencyToken, rows });

  const response = await fetch(`/api/v1/personnel-files/${personnelFileId}/documents`, {
    method: "PUT",
    body: formData
  });

  if (!response.ok) {
    throw await response.json();
  }

  return response.json();
}
```

## Response Handling

Successful `PUT /documents` returns:

- `PersonnelFileSectionResult<IReadOnlyCollection<PersonnelFileDocumentMetadataResponse>>`

Frontend must treat the response as the new source of truth for the tab.

Update local state from:

- `data`
- `personnelFileConcurrencyToken`

Do not keep using the previous concurrency token after a successful write.

## Important Field Semantics

### `personnelFileConcurrencyToken`

`PUT /documents` uses the parent personnel file concurrency token.

Frontend must:

- read the current token from the personnel file shell or previous section result
- send it on every `PUT /documents`
- replace local token with the one returned by the response

### `fileUrl`

`fileUrl` is the resolved read URL for the document.

Frontend should:

- use it directly for preview/open/download
- handle `null` defensively, even though standard successful flows normally return a value

### `fileName` and `contentType`

These fields come from the stored uploaded file.

Frontend must not treat them as independent editable metadata.

## Validation and Error Expectations

Frontend should expect request rejection in these cases:

- new item without file
- duplicated `documentPublicId` in the same manifest
- duplicated `fileKey`
- `fileKey` declared in manifest without matching uploaded file part
- uploaded file part not referenced by any `fileKey`
- invalid date ranges
- invalid file type
- invalid file signature
- file too large
- stale `concurrencyToken`

Allowed file formats:

- `.pdf`
- `.jpg`
- `.jpeg`
- `.png`
- `.docx`

Maximum file size:

- `10 MiB`

## Migration From Previous Frontend Flow

If frontend still uses per-document update actions, migrate to this behavior:

### Remove

- direct calls to `PATCH /api/v1/personnel-file-documents/{publicId}/file`
- direct calls to `PATCH /api/v1/personnel-file-documents/{publicId}/inactivate`
- any assumption that removing one row is a standalone server action

### Add

- one tab-level save action that sends the active collection via `PUT /documents`
- local diffing only to decide which rows include `fileKey`
- response-driven refresh of the documents list and parent concurrency token

### Keep

- `GET /documents` for reads
- `POST /documents` only if the product intentionally keeps a standalone create action

## Recommended Frontend Save Flow

1. Load shell with `GET /api/v1/personnel-files/{id}`.
2. Load documents with `GET /api/v1/personnel-files/{id}/documents`.
3. Let the user edit the active rows locally.
4. On save, build `FormData` with `concurrencyToken + manifestJson + changed/new files`.
5. Send `PUT /api/v1/personnel-files/{id}/documents`.
6. Replace local documents state from response `data`.
7. Replace local personnel file concurrency token from response `personnelFileConcurrencyToken`.

## QA Checklist For Frontend

- Existing document metadata edit works without selecting a new file.
- Existing document file replacement works in the same save action.
- New document creation works through the same save action.
- Removing a row from the submitted collection makes it disappear from the active UI after save.
- The UI stops calling removed `PATCH` routes.
- The UI uses the returned concurrency token after every successful save.
- The UI opens attachments using `fileUrl`.

## Final Integration Rule

For frontend, document updates are no longer item-scoped server actions.

The documents tab must now be integrated as a collection sync against:

- `PUT /api/v1/personnel-files/{personnelFileId}/documents`
