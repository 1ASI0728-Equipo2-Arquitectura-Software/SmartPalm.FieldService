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
- `POST /api/v1/field/visits/{visitId}/report-draft` (sin cuerpo), para que el agrónomo asignado y afiliado genere un borrador de IA a partir de una visita completada con observaciones. No guarda ni publica el informe.
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

Para habilitar el borrador, inyecta `MINIMAX_API_KEY` como secreto de ejecución de Field y, opcionalmente, `MINIMAX_MODEL` (por defecto, `MiniMax-M3.1-Flash`). Si falta la clave, el borrador responde `502`; las otras operaciones de Field siguen disponibles. MiniMax recibe hasta 30 observaciones de la visita con sus referencias de sector y fecha. No incluyas datos personales en las observaciones de la prueba.

Si hay observaciones de categoría `Irrigation`, Field consulta la humedad del suelo de hasta tres sectores con `Ingestion__BaseUrl` (origen interno de confianza) usando el JWT original. `soilMoistureContext` separa la evidencia instrumental del hallazgo observado: compara las medianas del mismo sensor en las dos horas anteriores y posteriores, con al menos tres marcas distintas por lado. Los estados de comparación son `Compared`, `InsufficientData` y `Unavailable`. Para una comparación válida, Field consulta con el mismo JWT el umbral vigente del sensor utilizado. `levelBefore` y `levelAfter` se comparan con ese rango (límites inclusivos), mientras `direction` describe solo el cambio entre medianas. Un umbral faltante, inválido o no autorizado deja el nivel en `Unknown` sin ocultar el cambio observado. Field calcula y valida las cifras; la skill de MiniMax recibe evidencia derivada, no muestras crudas ni identificadores del sensor. Configura el mismo emisor, audiencia y firma JWT que usa Ingestion, y HTTPS cuando el enlace sale de la red privada de Docker. Si falta el destino o Ingestion falla, se devuelve el borrador con contexto explícitamente no disponible; nunca se sugiere una causa sin datos.

## Validación

```powershell
dotnet restore SmartPalm.FieldService.slnx
dotnet build SmartPalm.FieldService.slnx --no-restore
dotnet test SmartPalm.FieldService.slnx --no-build
```

## Despliegue en Coolify/Vultr

El microservicio usa PostgreSQL y RabbitMQ externos. PostgreSQL y RabbitMQ no deben quedar dentro de este compose de producción; ambos servicios deben ser accesibles desde la instancia de Vultr donde se ejecuta Coolify.

En Coolify, crear el recurso desde el repositorio usando **Dockerfile** como Build Pack y configurar estas variables de ejecución:

```text
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=http://+:8080
ConnectionStrings__FieldDatabase=Host=<ip_privada_postgresql>;Port=5432;Database=smartpalm_field;Username=<usuario_field>;Password=<secreto>
Jwt__Key=<clave compartida de al menos 32 caracteres>
Jwt__Issuer=smartpalm.identity
Jwt__Audience=smartpalm.clients
RabbitMq__Enabled=true
RabbitMq__Uri=amqp://<usuario>:<password>@<ip_privada_rabbitmq>:5672/
RabbitMq__Exchange=smartpalm.events
RabbitMq__Queue=smartpalm.field
```

Publicar la aplicación en el puerto interno `8080` y usar `/health/live` y `/health/ready` como health checks. No publicar el contenedor de Field directamente al público: el tráfico debe entrar por el API Gateway.

Como alternativa para una distribución Docker Compose, revisar `compose.production.yaml` y proporcionar los secretos desde `.env` o desde las variables de ejecución de la plataforma:

```powershell
docker compose --env-file .env -f compose.production.yaml config
docker compose --env-file .env -f compose.production.yaml up -d
```
