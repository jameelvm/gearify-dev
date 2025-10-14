# Troubleshooting

## Docker Issues

**Problem**: Services won't start
**Solution**:
- Check `docker logs <service-name>`
- Verify `.env` file exists and has correct values
- Run `make validate-env`

**Problem**: Port conflicts
**Solution**: Stop services using ports 4200, 8080, 5432, 6379, 4566

## LocalStack Issues

**Problem**: "License key invalid"
**Solution**: Verify `LOCALSTACK_API_KEY` in `.env` file

**Problem**: Resources not created
**Solution**:
- Check init script: `docker logs gearify-localstack`
- Manually re-run: `docker exec gearify-localstack bash /etc/localstack/init/ready.d/init-aws.sh`

## Windows-Specific Issues

**Problem**: Scripts won't execute
**Solution**:
- Use PowerShell: `.\scripts\clone-all.ps1`
- Or Git Bash: `bash scripts/clone-all.sh`

**Problem**: Line ending errors
**Solution**: Configure Git:
```bash
git config core.autocrlf input
```

## Service Communication Issues

**Problem**: Services can't connect to each other
**Solution**:
- All services must be on `gearify-network`
- Use service names (e.g., `http://catalog-svc:80`) not localhost

## Database Issues

**Problem**: DynamoDB table not found
**Solution**:
- Verify LocalStack is healthy
- Re-seed: `make seed-clean`

**Problem**: PostgreSQL connection refused
**Solution**:
- Check if postgres container is running: `docker ps | grep postgres`
- Verify connection string in `.env`
