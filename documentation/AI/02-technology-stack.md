# AI Technology Stack for Gearify

Recommended AI/ML services and libraries integrated with your current .NET & AWS stack.

## Current Gearify Tech Stack

```
Backend:      .NET 8.0 (C#)
Cloud:        AWS (LocalStack for dev)
Database:     DynamoDB, PostgreSQL
Message Queue: SQS, SNS
Storage:      S3
Cache:        Redis
Frontend:     Angular
API Gateway:  YARP
```

## Recommended AI Technology Stack

### ☁️ AWS AI/ML Services (Primary)

All AWS AI services have .NET SDK support and LocalStack compatibility.

---

## 1. Product Recommendations

### **AWS Personalize** (Recommended)
**Use Case**: Product recommendations, personalized rankings

**Why AWS Personalize**:
- Native AWS integration (already using AWS)
- Fully managed (no ML expertise required)
- Real-time and batch recommendations
- Built-in recipes for e-commerce
- LocalStack Pro support for development

**Integration with Gearify**:
```csharp
// Install NuGet Package
dotnet add package AWSSDK.Personalize
dotnet add package AWSSDK.PersonalizeRuntime

// C# Implementation
using Amazon.PersonalizeRuntime;
using Amazon.PersonalizeRuntime.Model;

public class ProductRecommendationService
{
    private readonly IAmazonPersonalizeRuntime _personalizeClient;

    public ProductRecommendationService(IAmazonPersonalizeRuntime personalizeClient)
    {
        _personalizeClient = personalizeClient;
    }

    public async Task<List<string>> GetRecommendations(
        string userId,
        string campaignArn,
        int numResults = 10)
    {
        var request = new GetRecommendationsRequest
        {
            CampaignArn = campaignArn,
            UserId = userId,
            NumResults = numResults
        };

        var response = await _personalizeClient.GetRecommendationsAsync(request);
        return response.ItemList.Select(item => item.ItemId).ToList();
    }

    public async Task<List<string>> GetSimilarItems(
        string productId,
        string campaignArn,
        int numResults = 5)
    {
        var request = new GetRecommendationsRequest
        {
            CampaignArn = campaignArn,
            ItemId = productId,
            NumResults = numResults
        };

        var response = await _personalizeClient.GetRecommendationsAsync(request);
        return response.ItemList.Select(item => item.ItemId).ToList();
    }
}

// Startup.cs
services.AddAWSService<IAmazonPersonalizeRuntime>();
services.AddScoped<IProductRecommendationService, ProductRecommendationService>();
```

**Data Pipeline**:
```
DynamoDB (Orders, Products, User Interactions)
    → Export to S3 (daily batch)
    → AWS Personalize Dataset Import
    → Train Recommendation Model
    → Deploy Campaign
    → Real-time API (GetRecommendations)
```

**Recipes for Gearify**:
- `aws-user-personalization` - General recommendations
- `aws-similar-items` - Similar products
- `aws-personalized-ranking` - Rerank search results
- `aws-popularity-count` - Trending products

**Cost**:
- Training: $0.24/hour
- Inference: $0.20 per 1000 requests
- Free tier: 2 months, 20 hours training, 50K requests

---

### **Alternative: ML.NET** (Custom Solution)
**Use Case**: If you want full control or offline recommendations

```csharp
// Install NuGet Package
dotnet add package Microsoft.ML
dotnet add package Microsoft.ML.Recommender

public class ProductRecommendationEngine
{
    private readonly MLContext _mlContext;

    public void TrainModel(string trainingDataPath, string modelPath)
    {
        _mlContext = new MLContext();

        // Load data from DynamoDB → CSV/Parquet
        IDataView trainingData = _mlContext.Data.LoadFromTextFile<ProductRating>(
            trainingDataPath,
            hasHeader: true,
            separatorChar: ',');

        // Build pipeline
        var pipeline = _mlContext.Transforms.Conversion
            .MapValueToKey("userId", "UserIdEncoded")
            .Append(_mlContext.Transforms.Conversion
                .MapValueToKey("productId", "ProductIdEncoded"))
            .Append(_mlContext.Recommendation()
                .Trainers.MatrixFactorization(
                    labelColumnName: "Rating",
                    matrixColumnIndexColumnName: "UserIdEncoded",
                    matrixRowIndexColumnName: "ProductIdEncoded",
                    numberOfIterations: 20,
                    approximationRank: 100));

        // Train
        var model = pipeline.Fit(trainingData);

        // Save
        _mlContext.Model.Save(model, trainingData.Schema, modelPath);
    }

    public List<string> PredictRecommendations(string userId, int topN = 10)
    {
        // Load model and predict
        // Implementation details...
    }
}
```

**Pros**: Full control, no external API costs, offline capable
**Cons**: You maintain training pipeline, model versioning, scaling

---

## 2. Natural Language Processing (NLP)

### **AWS Comprehend** (Recommended)
**Use Case**: Search query understanding, sentiment analysis, entity extraction

**Features for Gearify**:
- Entity recognition (brand, price, category)
- Sentiment analysis (reviews)
- Language detection
- Key phrase extraction

```csharp
// Install NuGet Package
dotnet add package AWSSDK.Comprehend

using Amazon.Comprehend;
using Amazon.Comprehend.Model;

public class SearchQueryAnalyzer
{
    private readonly IAmazonComprehend _comprehend;

    public async Task<SearchIntent> AnalyzeQuery(string query)
    {
        // Entity extraction
        var entityRequest = new DetectEntitiesRequest
        {
            Text = query,
            LanguageCode = "en"
        };
        var entities = await _comprehend.DetectEntitiesAsync(entityRequest);

        // Extract price range
        var priceEntity = entities.Entities
            .FirstOrDefault(e => e.Type == EntityType.QUANTITY &&
                               query.Contains("rupee", StringComparison.OrdinalIgnoreCase));

        // Extract brands
        var brandEntity = entities.Entities
            .Where(e => e.Type == EntityType.ORGANIZATION ||
                       e.Type == EntityType.COMMERCIAL_ITEM)
            .Select(e => e.Text)
            .ToList();

        return new SearchIntent
        {
            OriginalQuery = query,
            ExtractedBrands = brandEntity,
            PriceRange = ExtractPriceRange(query),
            Intent = DetermineIntent(query) // Buy/Browse/Compare
        };
    }

    public async Task<SentimentScore> AnalyzeReview(string reviewText)
    {
        var request = new DetectSentimentRequest
        {
            Text = reviewText,
            LanguageCode = "en"
        };

        var response = await _comprehend.DetectSentimentAsync(request);

        return new SentimentScore
        {
            Sentiment = response.Sentiment.Value,
            PositiveScore = response.SentimentScore.Positive,
            NegativeScore = response.SentimentScore.Negative,
            NeutralScore = response.SentimentScore.Neutral
        };
    }
}
```

**Cost**:
- $0.0001 per unit (100 characters)
- Free tier: 50K units/month for 12 months

---

### **Alternative: Elasticsearch with NLP Plugins**
For search-specific NLP

```
Elasticsearch 8.x + NLP plugins
- Query expansion
- Synonym handling
- Fuzzy matching
- Language analyzers
```

**Integration**:
```csharp
dotnet add package NEST // Elasticsearch .NET client

var settings = new ConnectionSettings(new Uri("http://localhost:9200"))
    .DefaultIndex("products");

var client = new ElasticClient(settings);

// Search with NLP
var searchResponse = await client.SearchAsync<Product>(s => s
    .Query(q => q
        .MultiMatch(m => m
            .Query("lightweight bat for teenager")
            .Fields(f => f
                .Field(p => p.Name, boost: 2)
                .Field(p => p.Description)
                .Field(p => p.Category))
            .Fuzziness(Fuzziness.Auto)
            .Analyzer("english")))
);
```

---

## 3. Computer Vision

### **AWS Rekognition** (Recommended)
**Use Case**: Visual search, image tagging, quality control

```csharp
// Install NuGet Package
dotnet add package AWSSDK.Rekognition

using Amazon.Rekognition;
using Amazon.Rekognition.Model;

public class ImageAnalysisService
{
    private readonly IAmazonRekognition _rekognition;

    // Detect cricket equipment in image
    public async Task<List<Label>> DetectCricketEquipment(string s3Bucket, string s3Key)
    {
        var request = new DetectLabelsRequest
        {
            Image = new Image
            {
                S3Object = new S3Object
                {
                    Bucket = s3Bucket,
                    Name = s3Key
                }
            },
            MaxLabels = 10,
            MinConfidence = 75F
        };

        var response = await _rekognition.DetectLabelsAsync(request);
        return response.Labels;
    }

    // Find similar products by image
    public async Task<List<string>> FindSimilarProducts(
        string s3Bucket,
        string uploadedImageKey)
    {
        // Use Rekognition Custom Labels for cricket equipment
        var request = new DetectCustomLabelsRequest
        {
            ProjectVersionArn = "arn:aws:rekognition:...:project/cricket-equipment/...",
            Image = new Image
            {
                S3Object = new S3Object
                {
                    Bucket = s3Bucket,
                    Name = uploadedImageKey
                }
            },
            MinConfidence = 70F
        };

        var response = await _rekognition.DetectCustomLabelsAsync(request);

        // Map detected labels to products in DynamoDB
        var detectedEquipment = response.CustomLabels
            .Where(l => l.Confidence > 80)
            .Select(l => l.Name)
            .ToList();

        return await FindProductsByLabels(detectedEquipment);
    }

    // Image quality check
    public async Task<bool> IsImageQualityGood(string s3Bucket, string s3Key)
    {
        var request = new DetectModerationLabelsRequest
        {
            Image = new Image
            {
                S3Object = new S3Object { Bucket = s3Bucket, Name = s3Key }
            }
        };

        var response = await _rekognition.DetectModerationLabelsAsync(request);

        // Check for blur, low quality, etc.
        return !response.ModerationLabels.Any();
    }
}
```

**Custom Labels Training**:
1. Collect images of cricket equipment (bats, balls, pads, etc.)
2. Label them in Rekognition console
3. Train custom model
4. Deploy endpoint
5. Use in .NET app

**Cost**:
- $1.00 per 1000 images processed
- Custom Labels training: $1.00/hour
- Free tier: 5000 images/month for 12 months

---

## 4. Conversational AI (Chatbot)

### **AWS Lex V2** (Recommended)
**Use Case**: Customer support chatbot, order tracking, product discovery

```csharp
// Install NuGet Package
dotnet add package AWSSDK.LexRuntimeV2

using Amazon.LexRuntimeV2;
using Amazon.LexRuntimeV2.Model;

public class ChatbotService
{
    private readonly IAmazonLexRuntimeV2 _lexClient;

    public async Task<string> ProcessMessage(
        string userId,
        string message,
        string sessionId)
    {
        var request = new RecognizeTextRequest
        {
            BotId = "YOUR_BOT_ID",
            BotAliasId = "YOUR_ALIAS_ID",
            LocaleId = "en_US",
            SessionId = sessionId,
            Text = message
        };

        var response = await _lexClient.RecognizeTextAsync(request);

        // Process intent
        var intent = response.SessionState.Intent.Name;

        return intent switch
        {
            "FindProduct" => await HandleProductSearch(response),
            "TrackOrder" => await HandleOrderTracking(response),
            "SizeGuide" => await HandleSizeGuide(response),
            _ => response.Messages.FirstOrDefault()?.Content ?? "I didn't understand that."
        };
    }

    private async Task<string> HandleProductSearch(RecognizeTextResponse response)
    {
        var slots = response.SessionState.Intent.Slots;

        var category = slots.ContainsKey("Category")
            ? slots["Category"]?.Value?.InterpretedValue
            : null;
        var budget = slots.ContainsKey("Budget")
            ? slots["Budget"]?.Value?.InterpretedValue
            : null;

        // Query DynamoDB for products
        var products = await _productService.SearchProducts(category, budget);

        return FormatProductResponse(products);
    }
}
```

**Lex Bot Intents for Gearify**:
- `FindProduct` - Product discovery
- `TrackOrder` - Order status
- `SizeGuide` - Size recommendations
- `ReturnPolicy` - Policy questions
- `CompareProducts` - Product comparison
- `CheckStock` - Availability check

**Lex + Lambda Integration**:
```csharp
// Lambda function (.NET 8) for Lex fulfillment
public class LexFulfillmentFunction
{
    public async Task<LexV2Response> FunctionHandler(
        LexV2Event lexEvent,
        ILambdaContext context)
    {
        var intent = lexEvent.SessionState.Intent.Name;

        return intent switch
        {
            "FindProduct" => await FindProducts(lexEvent),
            "TrackOrder" => await TrackOrder(lexEvent),
            _ => new LexV2Response { /* ... */ }
        };
    }
}
```

**Cost**:
- $0.00075 per voice request
- $0.004 per text request
- Free tier: 10,000 text requests/month

---

## 5. Demand Forecasting

### **AWS Forecast** (Recommended)
**Use Case**: Inventory planning, demand prediction

```csharp
// Install NuGet Package
dotnet add package AWSSDK.ForecastService

using Amazon.ForecastService;
using Amazon.ForecastService.Model;

public class DemandForecastService
{
    private readonly IAmazonForecastService _forecast;

    // Create forecast
    public async Task<string> CreateForecast(
        string productId,
        string forecastHorizon = "30") // 30 days
    {
        // 1. Upload historical data to S3
        var historicalData = await ExportSalesData(productId);
        var s3Key = await UploadToS3(historicalData);

        // 2. Import dataset
        var datasetImport = new CreateDatasetImportJobRequest
        {
            DatasetArn = "arn:aws:forecast:...:dataset/cricket-sales",
            DataSource = new DataSource
            {
                S3Config = new S3Config
                {
                    Path = $"s3://gearify-forecast/{s3Key}",
                    RoleArn = "arn:aws:iam:...:role/ForecastRole"
                }
            }
        };

        await _forecast.CreateDatasetImportJobAsync(datasetImport);

        // 3. Train predictor (AutoML)
        var predictor = new CreateAutoPredictorRequest
        {
            PredictorName = $"cricket-demand-{productId}",
            ForecastHorizon = int.Parse(forecastHorizon),
            ForecastFrequency = "D", // Daily
            DataConfig = new DataConfig
            {
                DatasetGroupArn = "arn:aws:forecast:...:dataset-group/cricket"
            }
        };

        var predictorResponse = await _forecast.CreateAutoPredictorAsync(predictor);

        // 4. Create forecast
        var forecastRequest = new CreateForecastRequest
        {
            ForecastName = $"demand-forecast-{productId}",
            PredictorArn = predictorResponse.PredictorArn
        };

        var forecastResponse = await _forecast.CreateForecastAsync(forecastRequest);

        return forecastResponse.ForecastArn;
    }

    // Query forecast
    public async Task<Dictionary<DateTime, decimal>> GetForecast(
        string forecastArn,
        string productId)
    {
        var query = new QueryForecastRequest
        {
            ForecastArn = forecastArn,
            Filters = new Dictionary<string, string>
            {
                { "item_id", productId }
            }
        };

        var response = await _forecast.QueryForecastAsync(query);

        return response.Forecast.Predictions["p50"] // median forecast
            .ToDictionary(
                p => DateTime.Parse(p.Timestamp),
                p => decimal.Parse(p.Value));
    }
}
```

**Data Requirements**:
- Minimum 2 years of historical sales data
- Daily/Weekly/Monthly granularity
- Related time series (price, promotions, events)

**Cost**:
- Training: $0.24/hour
- Forecast generation: $0.60 per 1000 forecasts
- Storage: S3 costs

---

## 6. Fraud Detection

### **AWS Fraud Detector** (Recommended)
**Use Case**: Payment fraud, account fraud, review fraud

```csharp
// Install NuGet Package
dotnet add package AWSSDK.FraudDetector

using Amazon.FraudDetector;
using Amazon.FraudDetector.Model;

public class FraudDetectionService
{
    private readonly IAmazonFraudDetector _fraudDetector;

    public async Task<FraudScore> CheckOrderFraud(Order order)
    {
        var request = new GetEventPredictionRequest
        {
            DetectorId = "gearify-order-fraud",
            DetectorVersionId = "1",
            EventId = order.Id,
            EventTypeName = "order_placement",
            EventTimestamp = DateTime.UtcNow.ToString("o"),
            Entities = new List<Entity>
            {
                new Entity
                {
                    EntityType = "customer",
                    EntityId = order.UserId
                }
            },
            EventVariables = new Dictionary<string, string>
            {
                { "order_amount", order.TotalAmount.ToString() },
                { "payment_method", order.PaymentMethod },
                { "ip_address", order.IpAddress },
                { "email", order.Email },
                { "shipping_address", order.ShippingAddress },
                { "is_first_order", order.IsFirstOrder.ToString().ToLower() },
                { "device_fingerprint", order.DeviceFingerprint }
            }
        };

        var response = await _fraudDetector.GetEventPredictionAsync(request);

        var fraudScore = response.ModelScores
            .FirstOrDefault()?
            .Scores
            .FirstOrDefault()?
            .Value ?? 0;

        var riskLevel = fraudScore switch
        {
            >= 80 => RiskLevel.High,
            >= 50 => RiskLevel.Medium,
            _ => RiskLevel.Low
        };

        return new FraudScore
        {
            Score = fraudScore,
            RiskLevel = riskLevel,
            RuleResults = response.RuleResults,
            ShouldBlock = fraudScore >= 80,
            ShouldReview = fraudScore >= 50 && fraudScore < 80
        };
    }
}

// Middleware for fraud check
public class FraudDetectionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IFraudDetectionService _fraudService;

    public async Task InvokeAsync(HttpContext context)
    {
        if (context.Request.Path.StartsWithSegments("/api/orders") &&
            context.Request.Method == "POST")
        {
            var order = await DeserializeOrder(context.Request);
            var fraudScore = await _fraudService.CheckOrderFraud(order);

            if (fraudScore.ShouldBlock)
            {
                context.Response.StatusCode = 403;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "Order blocked due to fraud risk"
                });
                return;
            }

            if (fraudScore.ShouldReview)
            {
                // Flag for manual review
                await _fraudService.FlagForReview(order.Id);
            }
        }

        await _next(context);
    }
}
```

**Cost**:
- $7.50 per 1000 predictions
- Free tier: 10,000 predictions/month for 2 months

---

## 7. Background Jobs & Automation

### **Hangfire** (For Scheduled AI Tasks)
**Use Case**: Batch predictions, model retraining, report generation

```csharp
dotnet add package Hangfire
dotnet add package Hangfire.Redis.StackExchange

// Startup.cs
services.AddHangfire(config =>
    config.UseRedisStorage("localhost:6379"));
services.AddHangfireServer();

// Schedule AI jobs
public class AIJobScheduler
{
    public void ScheduleJobs()
    {
        // Daily demand forecast
        RecurringJob.AddOrUpdate<DemandForecastService>(
            "daily-demand-forecast",
            service => service.RunDailyForecast(),
            Cron.Daily(2)); // 2 AM

        // Weekly recommendation model retrain
        RecurringJob.AddOrUpdate<RecommendationService>(
            "weekly-model-retrain",
            service => service.RetrainModel(),
            Cron.Weekly(DayOfWeek.Sunday, 1)); // Sunday 1 AM

        // Hourly fraud model update
        RecurringJob.AddOrUpdate<FraudDetectionService>(
            "hourly-fraud-rules-update",
            service => service.UpdateFraudRules(),
            Cron.Hourly());
    }
}
```

---

## 8. Development & Testing

### **LocalStack Pro** (Local AWS AI Services)
**Use Case**: Local development of AWS AI features

```bash
# docker-compose.yml
localstack:
  image: localstack/localstack-pro:latest
  environment:
    - LOCALSTACK_AUTH_TOKEN=${LOCALSTACK_API_KEY}
    - SERVICES=personalize,comprehend,rekognition,lex,forecast,frauddetector
    - DEBUG=1
  ports:
    - "4566:4566"
```

**Supported AI Services in LocalStack Pro**:
- ✅ Comprehend (NLP)
- ✅ Rekognition (Computer Vision)
- ✅ Textract (OCR)
- ⚠️ Personalize (Limited)
- ⚠️ Lex (Limited)
- ❌ Forecast (Not supported - use mocks)
- ❌ Fraud Detector (Not supported - use mocks)

**For Unsupported Services**: Create mock implementations for local dev

```csharp
public interface IForecastService
{
    Task<Dictionary<DateTime, decimal>> GetForecast(string productId);
}

// Production
public class AwsForecastService : IForecastService { /* ... */ }

// Development (mock)
public class MockForecastService : IForecastService
{
    public Task<Dictionary<DateTime, decimal>> GetForecast(string productId)
    {
        // Return random but realistic forecast data
        return Task.FromResult(GenerateMockForecast());
    }
}

// Startup.cs
if (env.IsDevelopment())
    services.AddScoped<IForecastService, MockForecastService>();
else
    services.AddScoped<IForecastService, AwsForecastService>();
```

---

## Summary: Technology Choices

| Feature | Primary Tech | Alternative | Complexity |
|---------|-------------|-------------|------------|
| **Product Recommendations** | AWS Personalize | ML.NET | Medium |
| **NLP/Search** | AWS Comprehend + Elasticsearch | Custom NLP | Medium |
| **Computer Vision** | AWS Rekognition | Custom CNN | High |
| **Chatbot** | AWS Lex V2 | Dialogflow | Medium |
| **Demand Forecasting** | AWS Forecast | ML.NET Time Series | High |
| **Fraud Detection** | AWS Fraud Detector | Custom Rules + ML.NET | Medium |
| **Background Jobs** | Hangfire + Redis | AWS Step Functions | Low |
| **Caching** | Redis (existing) | - | - |
| **Event Processing** | SQS/SNS (existing) | - | - |

---

## Cost Estimation (Monthly - 10K active users)

| Service | Usage | Cost |
|---------|-------|------|
| AWS Personalize | 100K recommendations | $20 |
| AWS Comprehend | 500K requests | $50 |
| AWS Rekognition | 50K images | $50 |
| AWS Lex | 50K chat messages | $200 |
| AWS Forecast | Daily forecasts | $50 |
| AWS Fraud Detector | 10K checks | $75 |
| **Total** | | **~$445/month** |

**Note**: Most services have free tiers that cover initial development and testing.

---

**Next**: See [Implementation Roadmap](./03-implementation-roadmap.md) for phased rollout
