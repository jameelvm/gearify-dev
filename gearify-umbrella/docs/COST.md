# Cost Estimation & Optimization

## Local Development Costs

### LocalStack Pro
- **Cost**: $50/month per developer
- **Alternative**: Free tier (limited services, no Cognito)

### Developer Machine
- **Docker Desktop**: Free for personal use, $5-21/user/month for business
- **Hardware**: Requires 16GB+ RAM, SSD recommended

**Total Local**: ~$50-70/month per developer

## AWS Production Costs (Monthly Estimates)

### Compute - EKS
- **Control Plane**: $73/month (per cluster)
- **EC2 Worker Nodes**:
  - 3x t3.large (2 vCPU, 8GB): ~$150/month
  - Auto-scaling group: +$0-200/month (peak traffic)
- **Load Balancer**: $20-30/month
- **Data Transfer**: $50-100/month

**EKS Subtotal**: ~$293-553/month

### Databases & Storage

**DynamoDB**:
- On-demand mode: $1.25/million write requests, $0.25/million read requests
- Storage: $0.25/GB/month
- Estimate: $50-200/month (depends on traffic)

**RDS PostgreSQL** (db.t3.medium):
- Instance: $65/month
- Storage (100GB SSD): $11.50/month
- Backups: $10/month
- **Subtotal**: ~$87/month

**ElastiCache Redis** (cache.t3.micro):
- Instance: $17/month
- **Subtotal**: ~$17/month

**S3**:
- Storage: $0.023/GB (first 50TB)
- Requests: $0.005/1,000 PUT, $0.0004/1,000 GET
- Estimate: $20-50/month (10-50GB images)

**Databases/Storage Total**: ~$174-354/month

### Messaging & Queues

**SQS**:
- First 1M requests/month: Free
- After: $0.40/million requests
- Estimate: $10-30/month

**SNS**:
- First 1M publishes/month: Free
- After: $0.50/million
- Estimate: $5-15/month

**Messaging Total**: ~$15-45/month

### Observability

**CloudWatch**:
- Logs: $0.50/GB ingestion, $0.03/GB storage
- Metrics: $0.30/metric/month
- Estimate: $50-150/month

**X-Ray** (tracing):
- $5/million traces
- Estimate: $10-30/month

**Observability Total**: ~$60-180/month

### Other Services

**Cognito**:
- MAU pricing: Free for first 50,000, then $0.0055/MAU
- SMS MFA: $0.00645/SMS
- Estimate: $0-50/month

**Secrets Manager**:
- $0.40/secret/month
- ~10 secrets: $4/month

**Parameter Store**: Free (Standard)

**Route 53**:
- Hosted zone: $0.50/month
- Queries: $0.40/million
- Estimate: $5/month

**Other Total**: ~$9-59/month

## Total AWS Production Estimate

| Category | Low | High |
|----------|-----|------|
| Compute (EKS) | $293 | $553 |
| Databases/Storage | $174 | $354 |
| Messaging | $15 | $45 |
| Observability | $60 | $180 |
| Other Services | $9 | $59 |
| **TOTAL** | **$551** | **$1,191** |

**Typical**: ~$700-900/month for moderate traffic (10K orders/month)

## Cost Optimization Strategies

### 1. Right-Size Resources
- Start with smaller instances (t3.medium vs t3.large)
- Monitor CPU/memory usage
- Scale up only when needed

### 2. Reserved Instances / Savings Plans
- 1-year commitment: 30-40% savings
- 3-year commitment: 50-60% savings
- Apply to EC2, RDS, ElastiCache

### 3. DynamoDB Optimization
- Use on-demand for unpredictable workloads
- Switch to provisioned capacity for steady traffic (cheaper)
- Enable auto-scaling on provisioned tables

### 4. S3 Lifecycle Policies
- Move old images to S3 Glacier: $0.004/GB
- Delete temporary files after 30 days
- Use S3 Intelligent-Tiering

### 5. CloudWatch Log Retention
- Reduce retention from 365 to 30 days
- Export old logs to S3
- Savings: 70-90% on log storage

### 6. Cache Aggressively
- Use Redis for frequently accessed data
- Reduce DynamoDB/RDS queries
- Set appropriate TTLs

### 7. CDN for Static Assets
- Use CloudFront for product images
- Reduce S3 GET requests by 80-90%
- Lower data transfer costs

## Scaling Considerations

### Low Traffic (< 1K orders/month)
- **Cost**: ~$400-600/month
- Single EKS node, smaller RDS instance

### Moderate Traffic (10K orders/month)
- **Cost**: ~$700-900/month
- 2-3 EKS nodes, auto-scaling enabled

### High Traffic (100K orders/month)
- **Cost**: ~$2,000-3,500/month
- 5-10 EKS nodes, larger RDS, DynamoDB provisioned capacity

### Very High Traffic (1M+ orders/month)
- **Cost**: $10,000+/month
- Multi-region, sharding, dedicated support

## Monitoring Costs

### AWS Cost Explorer
- Free tool to track spending
- Set budgets and alerts

### Third-Party Tools
- CloudHealth (free tier available)
- CloudCheckr
- Datadog (includes APM + cost monitoring)

## Cost Alerts

Set up alerts when spending exceeds:
- 50% of monthly budget
- 80% of monthly budget
- 100% of monthly budget

```bash
aws budgets create-budget \
  --account-id 123456789012 \
  --budget file://budget.json \
  --notifications-with-subscribers file://notifications.json
```
