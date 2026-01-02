# Amazon Bedrock - Quick Reference Guide

Fast reference for Amazon Bedrock integration in Gearify e-commerce platform.

## What is Amazon Bedrock?

Amazon Bedrock is AWS's fully managed service providing access to foundation models (FMs) from leading AI companies through a single API.

**Key Benefit**: Use generative AI (like ChatGPT/Claude) without managing infrastructure, with data staying in your AWS account.

---

## Available Models & When to Use

| Model | Provider | Speed | Cost | Best For |
|-------|----------|-------|------|----------|
| **Claude 3.5 Sonnet** | Anthropic | Medium | $$$ | Complex reasoning, best quality content |
| **Claude 3 Haiku** | Anthropic | Fast | $ | Simple tasks, high volume |
| **Llama 3.1 (70B)** | Meta | Fast | $ | Open-source, general purpose |
| **Titan Text** | Amazon | Fast | $ | AWS-native, cost-effective |
| **Stable Diffusion** | Stability AI | Medium | $$ | Image generation |

### Cost per 1K tokens:
- **Claude 3.5 Sonnet**: $0.003 input / $0.015 output
- **Claude 3 Haiku**: $0.00025 input / $0.00125 output
- **Llama 3**: $0.00099 (both)
- **Titan Text**: $0.0005 input / $0.0015 output

---

## Use Cases in Gearify

### 1. Product Description Generator ✍️

**Replace**: Manual copywriting
**Save**: 30-60 minutes per product
**Quality**: SEO-optimized, consistent, professional

```csharp
// Generate description
var description = await _descriptionService.GenerateDescriptionAsync(product);

// Returns: long description, short description, bullet points, SEO keywords
```

**Cost**: ~$0.01 per product description

---

### 2. Intelligent Chatbot 💬

**Replace**: AWS Lex or manual customer support
**Benefits**:
- 24/7 availability
- Understands cricket terminology
- Product recommendations
- Order tracking
- Multi-turn conversations

```csharp
var response = await _chatbotService.HandleMessageAsync(userId, "I need a bat for my 12 year old", sessionId);
// Bot: "Great! For a 12-year-old, I recommend a Size 5 bat weighing 900-1100g..."
```

**Cost**: ~$0.012 per conversation (avg 400 tokens)
**Handles**: 70-80% of customer queries automatically

---

### 3. Review Summarization 📊

**Replace**: Manual review reading
**Benefits**:
- Instant insights from hundreds of reviews
- Pros/cons extraction
- Key themes identification
- Buying recommendations

```csharp
var summary = await _reviewAnalysisService.SummarizeReviewsAsync(productId);
// Returns: summary, pros, cons, key themes, buying recommendation
```

**Cost**: ~$0.006 per product (500 reviews)

---

### 4. Email Marketing Content 📧

**Generate**:
- Abandoned cart emails
- Product launch announcements
- Promotional campaigns
- Personalized recommendations

```csharp
var emailContent = await _emailGenerator.GenerateAbandonedCartEmailAsync(user, cartItems);
// Returns: subject line + HTML body
```

**Cost**: ~$0.003 per email

---

### 5. SEO Optimization 🔍

**Generate**:
- Meta descriptions
- Alt text for images
- Product titles
- Blog content

```csharp
var metaDesc = await _descriptionService.GenerateSEOMetaDescriptionAsync(product);
// Returns: 150-160 character optimized meta description
```

---

### 6. Multi-Language Support 🌍

**Translate** product content to:
- Hindi
- Tamil
- Bengali
- Other regional languages

```csharp
var translations = await _descriptionService.GenerateMultiLanguageDescriptionAsync(
    product,
    new[] { "hi", "ta", "bn" }
);
```

---

## Quick Setup

### 1. Install NuGet Package

```bash
dotnet add package AWSSDK.BedrockRuntime
```

### 2. Configure Services (Startup.cs)

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // AWS Bedrock client
    services.AddAWSService<IAmazonBedrockRuntime>();

    // Your Bedrock services
    services.AddScoped<IProductDescriptionService, ProductDescriptionService>();
    services.AddScoped<IChatbotService, BedrockChatbotService>();
    services.AddScoped<IReviewAnalysisService, ReviewAnalysisService>();
}
```

### 3. Add Configuration (appsettings.json)

```json
{
  "AWS": {
    "Bedrock": {
      "DefaultModel": "anthropic.claude-3-5-sonnet-20240620-v1:0",
      "FastModel": "anthropic.claude-3-haiku-20240307-v1:0"
    }
  }
}
```

### 4. Basic Usage Example

```csharp
public class ProductDescriptionService
{
    private readonly IAmazonBedrockRuntime _bedrock;

    public async Task<string> GenerateAsync(string prompt)
    {
        var request = new InvokeModelRequest
        {
            ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(
                JsonSerializer.Serialize(new
                {
                    anthropic_version = "bedrock-2023-05-31",
                    max_tokens = 1000,
                    messages = new[] { new { role = "user", content = prompt } }
                })
            ))
        };

        var response = await _bedrock.InvokeModelAsync(request);
        var result = JsonSerializer.Deserialize<ClaudeResponse>(response.Body);

        return result.Content[0].Text;
    }
}
```

---

## Cost Comparison

### Traditional Approach vs Bedrock

| Task | Manual Cost | Bedrock Cost | Savings |
|------|-------------|--------------|---------|
| Product description | ₹500 (copywriter) | ₹0.80 | 99.8% |
| Customer support (per query) | ₹50 (agent) | ₹1 | 98% |
| Review analysis | ₹200 (analyst) | ₹0.50 | 99.75% |
| Email copywriting | ₹300 (marketer) | ₹0.25 | 99.9% |

### Monthly Cost Estimate (Gearify Scale)

**Assumptions**: 1000 products, 10K users, 500 chats/day

| Feature | Usage | Cost/Month |
|---------|-------|------------|
| Product descriptions | 100 new products | $10 |
| Chatbot | 15,000 conversations | $180 |
| Review summaries | 500 products | $6 |
| Email generation | 5,000 emails | $4 |
| **Total** | | **~$200/month** |

**ROI**: Replaces ~$50,000/month in manual work with $200/month AI service.

---

## Model Selection Guide

### Use Claude 3.5 Sonnet When:
- ✅ Generating customer-facing content (product descriptions, emails)
- ✅ Complex chatbot conversations
- ✅ High-quality output is critical
- ✅ Reasoning and understanding context

### Use Claude 3 Haiku When:
- ✅ High-volume simple tasks (review summaries)
- ✅ Quick responses needed (chatbot for simple queries)
- ✅ Cost is a primary concern
- ✅ Bulk processing (email generation)

### Use Llama 3 When:
- ✅ Open-source preference
- ✅ General text generation
- ✅ Balanced cost and quality

### Use Titan When:
- ✅ Maximum cost efficiency
- ✅ AWS-native preference
- ✅ Simple text tasks
- ✅ Embeddings for search

---

## Best Practices

### 1. Caching Strategy
```csharp
// Cache product descriptions for 7 days
await _cache.SetStringAsync(
    $"desc:{productId}",
    description,
    new DistributedCacheEntryOptions {
        AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
    }
);
```

### 2. Cost Control
```csharp
// Track token usage
_costTracker.TrackUsage(
    modelId: "claude-3-5-sonnet",
    inputTokens: result.Usage.InputTokens,
    outputTokens: result.Usage.OutputTokens,
    feature: "product-description"
);
```

### 3. Error Handling
```csharp
try
{
    return await InvokeBedrockAsync(prompt);
}
catch (AmazonBedrockException ex)
{
    _logger.LogError(ex, "Bedrock invocation failed");
    // Fallback to template or manual process
    return GenerateFallbackContent(product);
}
```

### 4. Rate Limiting
```csharp
// Implement rate limiting to stay within quotas
private readonly SemaphoreSlim _rateLimiter = new SemaphoreSlim(10, 10);

public async Task<string> GenerateAsync(string prompt)
{
    await _rateLimiter.WaitAsync();
    try
    {
        return await InvokeBedrockAsync(prompt);
    }
    finally
    {
        _rateLimiter.Release();
    }
}
```

---

## Monitoring

### CloudWatch Metrics

```csharp
// Publish custom metrics
await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
{
    Namespace = "Gearify/Bedrock",
    MetricData = new List<MetricDatum>
    {
        new MetricDatum
        {
            MetricName = "TokensUsed",
            Value = totalTokens,
            Unit = StandardUnit.Count,
            Dimensions = new List<Dimension>
            {
                new Dimension { Name = "Model", Value = modelId },
                new Dimension { Name = "Feature", Value = "chatbot" }
            }
        }
    }
});
```

### Set Up Alerts

```bash
# Alert if daily cost exceeds $50
aws cloudwatch put-metric-alarm \
  --alarm-name bedrock-daily-cost-alert \
  --metric-name Cost \
  --namespace Gearify/Bedrock \
  --statistic Sum \
  --period 86400 \
  --threshold 50 \
  --comparison-operator GreaterThanThreshold
```

---

## Integration Checklist

- [ ] Install AWSSDK.BedrockRuntime NuGet package
- [ ] Configure AWS credentials (IAM role or access keys)
- [ ] Add Bedrock configuration to appsettings.json
- [ ] Implement ProductDescriptionService
- [ ] Implement ChatbotService
- [ ] Implement ReviewAnalysisService
- [ ] Set up Redis caching
- [ ] Configure cost tracking
- [ ] Set up CloudWatch monitoring
- [ ] Test with sample products
- [ ] Deploy to staging environment
- [ ] Monitor costs for 1 week
- [ ] Roll out to production

---

## Common Issues & Solutions

### Issue: "Model not found"
**Solution**: Check model ID spelling and ensure Bedrock is enabled in your AWS region
```csharp
// Correct model ID format
ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0"
```

### Issue: Rate limit exceeded
**Solution**: Implement exponential backoff and request batching
```csharp
var retryPolicy = Policy
    .Handle<ThrottlingException>()
    .WaitAndRetryAsync(3, retryAttempt =>
        TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)));
```

### Issue: High costs
**Solution**:
1. Use caching aggressively
2. Switch to cheaper models for simple tasks (Claude Haiku)
3. Implement request throttling
4. Set up cost alerts

### Issue: Slow response times
**Solution**:
1. Use faster models (Claude Haiku instead of Sonnet)
2. Reduce max_tokens parameter
3. Implement async processing for non-critical paths
4. Cache frequently requested content

---

## Security Considerations

### 1. IAM Permissions

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "bedrock:InvokeModel",
        "bedrock:InvokeModelWithResponseStream"
      ],
      "Resource": [
        "arn:aws:bedrock:us-east-1::foundation-model/anthropic.claude-3-5-sonnet-20240620-v1:0",
        "arn:aws:bedrock:us-east-1::foundation-model/anthropic.claude-3-haiku-20240307-v1:0"
      ]
    }
  ]
}
```

### 2. Data Privacy

✅ **Bedrock Benefits**:
- Your data stays in your AWS account
- Not used to train foundation models (Anthropic, Meta)
- Encrypted in transit and at rest
- Audit logs via CloudTrail

### 3. Content Filtering

```csharp
// Implement guardrails
var request = new InvokeModelRequest
{
    ModelId = modelId,
    GuardrailIdentifier = "your-guardrail-id",
    GuardrailVersion = "1",
    Body = requestBody
};
```

---

## Next Steps

1. **Read Full Documentation**: [bedrock-generative-ai.md](./features/bedrock-generative-ai.md)
2. **Review Implementation Roadmap**: See where Bedrock fits in phases
3. **Start Small**: Begin with product description generation
4. **Monitor Costs**: Track for first month, optimize
5. **Expand Usage**: Add chatbot, then review analysis

---

## Support Resources

- **AWS Bedrock Documentation**: https://docs.aws.amazon.com/bedrock/
- **Anthropic Claude Guide**: https://docs.anthropic.com/claude/docs
- **AWS .NET SDK**: https://docs.aws.amazon.com/sdk-for-net/
- **Bedrock Pricing**: https://aws.amazon.com/bedrock/pricing/

---

**Last Updated**: January 2026
**For detailed implementation**: See [features/bedrock-generative-ai.md](./features/bedrock-generative-ai.md)
