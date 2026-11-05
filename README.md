# SmartPalm Field Service

Microservicio del bounded context **Field Technical Management**. Gestiona visitas técnicas, inspecciones con sincronización offline e intervenciones agronómicas verificables. Conserva las rutas históricas de intervenciones y expone todo su contrato público bajo `/api/v1`.

## Rol en la plataforma

Field es la capa de seguimiento operativo que actúa después de monitorear un sector y cuando una recomendación o alerta requiere una acción humana. Los agrónomos registran visitas, inspecciones offline e intervenciones; los identificadores de las intervenciones resultantes pueden ser referenciados por los informes de Agronomy sin transferir la propiedad del agregado de intervención.

## Relaciones de integración

- **Identity Service:** proporciona usuarios y claims de roles utilizados para autorizar a agrónomos y administradores.
- **Crop y Agronomy:** proporcionan el contexto de plantación/sector y recomendación seleccionado por el trabajador de campo.
- **Alert/Ingestion:** pueden originar una visita de campo, pero Field no es dueño de lecturas de sensores, umbrales ni del ciclo de vida de las alertas.
- **Clientes web/móvil:** utilizan el Gateway y pueden sincronizar inspecciones después del trabajo offline.

## API pública

El contrato actual de Field está versionado bajo `/api/v1/field`:

- `/api/v1/field/visits` para la planificación y el ciclo de vida de visitas.
- `/api/v1/field/inspections` para inspecciones online y offline.
- `/api/v1/field/interventions` para la representación actual de intervenciones, filtrado, verificación y rechazo.

Las rutas heredadas de intervenciones bajo `/api/v1/sectors`, `/api/v1/plantations`, `/api/v1/recommendations` y `/api/v1/interventions` permanecen disponibles por compatibilidad. El namespace sin versionamiento `/api/field` no se publica.

## Ejecución local

```powershell
Copy-Item .env.example .env
docker compose -f compose.local.yaml up -d database
dotnet run --project SmartPalm.FieldService.API
```

API: `http://localhost:5107`; health: `/health/ready`. El perfil `messaging` levanta RabbitMQ para pruebas aisladas y el perfil `api` ejecuta también el servicio en Docker.

## Validación

```powershell
dotnet restore SmartPalm.FieldService.slnx
dotnet build SmartPalm.FieldService.slnx --no-restore
dotnet test SmartPalm.FieldService.slnx --no-build
```
