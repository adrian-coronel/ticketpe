# ERD — Sistema de Catálogo de Productos

**Versión:** 0.1.0
**Relacionado:** [`spec.md`](./spec.md) §7, §9

Decisiones aplicadas en este diagrama (ver `spec.md` §7 para el razonamiento):

- `Usuario`–`Role` es **1:N** (FK `RoleId` en `Usuario`), no many-to-many.
- `ProductoStaging` es el área de trabajo editable pre-commit; `FileProducto` es el linaje post-commit (qué fila de qué archivo originó/actualizó qué producto definitivo). Son tablas distintas.
- `AuditLog` se modela pero queda sin UI de consumo en v0.1.

```mermaid
erDiagram
    ROLE {
        int Id PK
        string Nombre
    }

    USUARIO {
        guid Id PK
        string Email
        string PasswordHash
        int RoleId FK
        bool Activo
        datetime CreatedAt
        datetime UpdatedAt
    }

    REFRESH_TOKEN {
        guid Id PK
        guid UsuarioId FK
        string Token
        datetime ExpiresAt
        datetime CreatedAt
        datetime RevokedAt
        string ReplacedByToken
    }

    CATEGORIA {
        int Id PK
        string Nombre
        string Descripcion
        bool Activa
    }

    PRODUCTO {
        guid Id PK
        string Sku
        string Nombre
        string Descripcion
        decimal Precio
        int Stock
        int CategoriaId FK
        datetime CreatedAt
        datetime UpdatedAt
    }

    FILE {
        guid Id PK
        string NombreArchivo
        string RutaAlmacenamiento
        guid UsuarioId FK
        string Status
        int TotalFilas
        int FilasProcesadas
        int FilasConError
        datetime CreatedAt
        datetime CompletedAt
    }

    PRODUCTO_STAGING {
        guid Id PK
        guid FileId FK
        int FilaOriginal
        string Sku
        string Nombre
        string Descripcion
        decimal Precio
        int Stock
        string CategoriaNombre
        bool TieneError
        bool EsManual
        datetime CreatedAt
        datetime UpdatedAt
    }

    FILE_PROCESSING_LOG {
        guid Id PK
        guid FileId FK
        int Fila
        string Columna
        string Mensaje
        datetime CreatedAt
    }

    FILE_PRODUCTO {
        guid Id PK
        guid FileId FK
        guid ProductoId FK
        int FilaOriginal
        string Accion
        datetime CreatedAt
    }

    AUDIT_LOG {
        guid Id PK
        guid UsuarioId FK
        string Entidad
        guid EntidadId
        string Accion
        string CambiosJson
        datetime CreatedAt
    }

    ROLE ||--o{ USUARIO : "tiene"
    USUARIO ||--o{ REFRESH_TOKEN : "emite"
    USUARIO ||--o{ FILE : "sube"
    USUARIO ||--o{ AUDIT_LOG : "genera"
    CATEGORIA ||--o{ PRODUCTO : "clasifica"
    FILE ||--o{ PRODUCTO_STAGING : "contiene"
    FILE ||--o{ FILE_PROCESSING_LOG : "registra"
    FILE ||--o{ FILE_PRODUCTO : "genera"
    PRODUCTO ||--o{ FILE_PRODUCTO : "originado_por"
```

## Notas de implementación

- `FILE.Status` es un enum: `Pendiente | Procesando | Completado | Fallido | Parcial` (ver `spec.md` §5.2).
- `PRODUCTO.Sku` es único; el commit (`POST /files/{id}/commit`) hace upsert por `Sku` (ver `spec.md` §7.5).
- `PRODUCTO_STAGING` vive solo mientras el `File` está en `Pendiente`/`Procesando`/revisión; se purga (o se conserva solo para auditoría, a decidir en Fase 6) tras el commit o descarte.
- `FILE_PRODUCTO.Accion` registra `Creado` o `Actualizado`, para distinguir altas de modificaciones en el linaje.
- Índices recomendados: `PRODUCTO(Sku)` único, `PRODUCTO_STAGING(FileId)`, `FILE_PROCESSING_LOG(FileId)`, `REFRESH_TOKEN(Token)` único.
