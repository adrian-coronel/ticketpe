# Contrato de eventos SignalR — `UploadHub`

**Versión:** 0.1.0
**Relacionado:** [`spec.md`](./spec.md) §5.2, [`openapi.yaml`](./openapi.yaml)

## Hub

- **Ruta:** `/hubs/upload`
- **Autenticación:** JWT (mismo access token usado en REST), enviado como `access_token` en la query string de la conexión (requerido por SignalR cuando no se pueden usar headers en WebSocket).
- **Agrupación:** cada conexión se une automáticamente al grupo `user:{usuarioId}` del usuario autenticado. El servidor **nunca** emite eventos de un `File` a un grupo distinto al de su propietario (`File.UsuarioId`), salvo rol `Admin` suscrito explícitamente a un grupo `file:{fileId}` adicional (ver `JoinFileGroup` más abajo).

## Métodos invocables por el cliente (client → hub)

### `JoinFileGroup(fileId: string)`
Suscribe la conexión actual al grupo `file:{fileId}` para recibir eventos de ese archivo específico. El hub valida que el usuario sea propietario del `File` o tenga rol `Admin`; si no, lanza `HubException("Forbidden")`.

### `LeaveFileGroup(fileId: string)`
Remueve la conexión del grupo `file:{fileId}`.

## Eventos emitidos por el servidor (hub → cliente)

Todos los eventos se emiten tanto al grupo `user:{usuarioId}` (propietario) como a `file:{fileId}` (suscriptores explícitos).

### `UploadProgress`

Emitido periódicamente durante el procesamiento (al completar cada lote de 500–1000 filas).

```json
{
  "fileId": "guid",
  "status": "Procesando",
  "totalFilas": 100000,
  "filasProcesadas": 15000,
  "filasConError": 42,
  "porcentaje": 15.0,
  "timestamp": "2026-10-02T14:30:00Z"
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `fileId` | `guid` | Identificador del `File` en procesamiento. |
| `status` | `string` | Siempre `"Procesando"` en este evento. |
| `totalFilas` | `int` | Total de filas detectadas en el archivo. |
| `filasProcesadas` | `int` | Filas ya insertadas en `ProductoStaging` (válidas + inválidas). |
| `filasConError` | `int` | Subconjunto de `filasProcesadas` que generó una entrada en `FileProcessingLog`. |
| `porcentaje` | `number` | `filasProcesadas / totalFilas * 100`, redondeado a 1 decimal. |
| `timestamp` | `string` (ISO 8601) | Momento de emisión del evento. |

### `UploadCompleted`

Emitido una sola vez, al finalizar el procesamiento sin error fatal (incluye los casos `Completado` y `Parcial`).

```json
{
  "fileId": "guid",
  "status": "Completado",
  "totalFilas": 100000,
  "filasProcesadas": 100000,
  "filasConError": 0,
  "timestamp": "2026-10-02T14:32:10Z"
}
```

`status` es `"Completado"` (0 errores) o `"Parcial"` (`filasConError > 0`). El cliente debe redirigir a la vista de staging/preview (`GET /files/{id}/staging`) al recibir este evento.

### `UploadFailed`

Emitido cuando el procesamiento se aborta por un error no recuperable (archivo corrupto, formato inválido, fallo de infraestructura).

```json
{
  "fileId": "guid",
  "status": "Fallido",
  "mensaje": "El archivo no tiene un formato .xlsx válido.",
  "timestamp": "2026-10-02T14:30:05Z"
}
```

| Campo | Tipo | Descripción |
|---|---|---|
| `fileId` | `guid` | Identificador del `File`. |
| `status` | `string` | Siempre `"Fallido"`. |
| `mensaje` | `string` | Descripción del error, apta para mostrar al usuario. |
| `timestamp` | `string` (ISO 8601) | Momento de emisión del evento. |

## Notas de implementación

- El worker debe limitar la frecuencia de `UploadProgress` a como máximo 1 evento por lote procesado (no por fila), para no saturar la conexión con 100k filas.
- Si la conexión SignalR se pierde durante el procesamiento, el cliente debe, al reconectar, hacer `GET /files/{id}` (REST) para obtener el estado actual antes de confiar en nuevos eventos — los eventos no son el único origen de verdad del estado.
- Reintentos de fila (ver `spec.md` RNF-6) no generan eventos adicionales; solo se reflejan en `filasConError` del próximo `UploadProgress`.
