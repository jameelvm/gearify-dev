# Debugging Guide

## Viewing Logs

### Seq (Structured Logs)
- URL: http://localhost:5341
- Filter by service: `@ServiceName = 'catalog-svc'`
- Filter by error: `@Level = 'Error'`

### Docker Logs
```bash
# Specific service
docker logs -f gearify-catalog-svc

# All services
make logs
```

## Distributed Tracing

### Jaeger
- URL: http://localhost:16686
- Search by service name
- View trace timeline
- Inspect span details

## Attaching Debugger

### .NET Services
1. Update docker-compose.yml to expose debugger port:
```yaml
ports:
  - "5001:80"
  - "5011:5011"  # Debugger port
```

2. Attach VS Code/Rider to container
3. Set breakpoints in code

## Common Issues

### Service Won't Start
```bash
# Check logs
docker logs gearify-catalog-svc

# Check health
docker inspect gearify-catalog-svc | grep Health
```

### Database Connection Errors
- Verify `.env` variables
- Check if infrastructure services are healthy:
```bash
docker ps | grep healthy
```

### LocalStack Issues
```bash
# Check status
make localstack-status

# View logs
make localstack-logs

# Restart
docker restart gearify-localstack
```
