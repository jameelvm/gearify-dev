# Amazon Bedrock - Generative AI Integration

Complete implementation guide for integrating Amazon Bedrock's generative AI capabilities into Gearify e-commerce platform.

## Overview

Amazon Bedrock provides access to high-performing foundation models (FMs) from leading AI companies through a single managed API, enabling powerful generative AI features without managing infrastructure.

### Business Value
- **Reduce Content Creation Costs**: Auto-generate product descriptions, emails, marketing copy
- **24/7 Intelligent Support**: AI chatbot that understands cricket and your products
- **Improve SEO**: Generate optimized content, alt texts, meta descriptions
- **Personalized Marketing**: Create customized emails and product recommendations
- **Faster Time-to-Market**: Launch new products with AI-generated content instantly

### Available Models in Bedrock

| Provider | Model | Best For | Cost (Input/Output per 1K tokens) |
|----------|-------|----------|-----------------------------------|
| **Anthropic** | Claude 3.5 Sonnet | Advanced reasoning, coding, analysis | $0.003 / $0.015 |
| **Anthropic** | Claude 3 Haiku | Fast, cost-effective tasks | $0.00025 / $0.00125 |
| **Meta** | Llama 3.1 (70B) | Open-source, general purpose | $0.00099 / $0.00099 |
| **Amazon** | Titan Text Premier | AWS-native, cost-effective | $0.0005 / $0.0015 |
| **Amazon** | Titan Embeddings | Vector embeddings for search | $0.0001 per 1K tokens |
| **Stability AI** | Stable Diffusion XL | Image generation | $0.04 per image |

---

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Gearify Services Layer                       │
│                                                                 │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐         │
│  │  Catalog     │  │ Notification │  │   Media      │         │
│  │  Service     │  │   Service    │  │  Service     │         │
│  └──────┬───────┘  └──────┬───────┘  └──────┬───────┘         │
│         │                  │                  │                 │
│         └──────────────────┼──────────────────┘                 │
│                            │                                     │
│                    ┌───────▼────────┐                          │
│                    │ BedrockService │                          │
│                    │  (Shared SDK)  │                          │
│                    └───────┬────────┘                          │
└────────────────────────────┼──────────────────────────────────┘
                             │
┌────────────────────────────▼──────────────────────────────────┐
│                    Amazon Bedrock                              │
│                                                                │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐          │
│  │  Claude 3.5 │  │   Llama 3   │  │ Titan Text   │          │
│  │   Sonnet    │  │             │  │              │          │
│  └─────────────┘  └─────────────┘  └──────────────┘          │
│                                                                │
│  ┌─────────────┐  ┌─────────────┐  ┌──────────────┐          │
│  │   Stable    │  │   Titan     │  │   Claude 3   │          │
│  │  Diffusion  │  │ Embeddings  │  │    Haiku     │          │
│  └─────────────┘  └─────────────┘  └──────────────┘          │
└────────────────────────────────────────────────────────────────┘
```

---

## Use Case 1: AI Product Description Generator

### Business Impact
- Save 30-60 minutes per product on manual copywriting
- Generate consistent, SEO-optimized descriptions
- Create variations for A/B testing
- Support multiple languages

### Implementation

#### 1. Service Layer

```csharp
// File: Gearify.CatalogService/Application/Services/ProductDescriptionService.cs

using Amazon.BedrockRuntime;
using Amazon.BedrockRuntime.Model;

public interface IProductDescriptionService
{
    Task<GeneratedDescription> GenerateDescriptionAsync(Product product);
    Task<List<string>> GenerateBulletPointsAsync(Product product);
    Task<string> GenerateSEOMetaDescriptionAsync(Product product);
    Task<Dictionary<string, string>> GenerateMultiLanguageDescriptionAsync(Product product, List<string> languages);
}

public class ProductDescriptionService : IProductDescriptionService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly IDistributedCache _cache;
    private readonly ILogger<ProductDescriptionService> _logger;

    // Use Claude 3.5 Sonnet for best quality
    private const string ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0";

    public ProductDescriptionService(
        IAmazonBedrockRuntime bedrock,
        IDistributedCache cache,
        ILogger<ProductDescriptionService> logger)
    {
        _bedrock = bedrock;
        _cache = cache;
        _logger = logger;
    }

    public async Task<GeneratedDescription> GenerateDescriptionAsync(Product product)
    {
        var cacheKey = $"desc:generated:{product.Id}";

        // Check cache first
        var cached = await _cache.GetStringAsync(cacheKey);
        if (cached != null)
        {
            return JsonSerializer.Deserialize<GeneratedDescription>(cached);
        }

        var prompt = BuildDescriptionPrompt(product);

        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            ContentType = "application/json",
            Accept = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 1000,
                temperature = 0.7, // Some creativity
                messages = new[]
                {
                    new
                    {
                        role = "user",
                        content = prompt
                    }
                }
            })))
        };

        try
        {
            var response = await _bedrock.InvokeModelAsync(request);

            using var reader = new StreamReader(response.Body);
            var responseBody = await reader.ReadToEndAsync();
            var result = JsonSerializer.Deserialize<ClaudeResponse>(responseBody);

            var generatedText = result.Content[0].Text;

            var description = new GeneratedDescription
            {
                ProductId = product.Id,
                LongDescription = ExtractSection(generatedText, "LONG_DESCRIPTION"),
                ShortDescription = ExtractSection(generatedText, "SHORT_DESCRIPTION"),
                BulletPoints = ExtractBulletPoints(generatedText),
                SEOKeywords = ExtractKeywords(generatedText),
                GeneratedAt = DateTime.UtcNow,
                ModelUsed = ModelId
            };

            // Cache for 7 days
            await _cache.SetStringAsync(
                cacheKey,
                JsonSerializer.Serialize(description),
                new DistributedCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromDays(7)
                });

            _logger.LogInformation(
                "Generated description for product {ProductId} using {Model}",
                product.Id, ModelId);

            return description;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate description for product {ProductId}", product.Id);
            throw;
        }
    }

    private string BuildDescriptionPrompt(Product product)
    {
        return $"""
            Generate professional, SEO-optimized product descriptions for this cricket equipment on Gearify.com (Indian e-commerce).

            PRODUCT DETAILS:
            Name: {product.Name}
            Category: {product.Category}
            Subcategory: {product.Subcategory}
            Brand: {product.Brand}
            Price: ₹{product.Price:N0}
            {(product.CompareAtPrice.HasValue ? $"Original Price: ₹{product.CompareAtPrice:N0}" : "")}
            {(product.DiscountPercentage.HasValue ? $"Discount: {product.DiscountPercentage}% OFF" : "")}

            REQUIREMENTS:

            1. LONG_DESCRIPTION (200-250 words):
               - Start with a compelling hook about the product
               - Highlight unique features and benefits
               - Explain who it's best suited for (skill level, playing style)
               - Include technical specifications naturally
               - End with a call-to-action
               - Use cricket terminology appropriately
               - Write for Indian audience (mention IPL, Ranji Trophy, etc. if relevant)
               - SEO: Include keywords like "{product.Category.ToLower()}", "{product.Brand.ToLower()}", "cricket equipment"

            2. SHORT_DESCRIPTION (50-75 words):
               - Concise summary highlighting main benefits
               - Perfect for product cards and listings

            3. BULLET_POINTS (5-7 points):
               - Key features in bullet format
               - Mix of technical specs and benefits
               - Each point 5-10 words

            4. SEO_KEYWORDS:
               - 10-15 relevant keywords for search optimization

            FORMAT YOUR RESPONSE AS:

            LONG_DESCRIPTION:
            [Your long description here]

            SHORT_DESCRIPTION:
            [Your short description here]

            BULLET_POINTS:
            • [Point 1]
            • [Point 2]
            ...

            SEO_KEYWORDS:
            [keyword1, keyword2, ...]

            TONE: Professional yet enthusiastic, cricket-knowledgeable, Indian context
            """;
    }

    public async Task<List<string>> GenerateBulletPointsAsync(Product product)
    {
        var prompt = $"""
            Generate 5-7 concise bullet points highlighting the key features of this cricket product:

            Product: {product.Name}
            Category: {product.Category}
            Brand: {product.Brand}

            Each bullet point should be 5-10 words. Mix technical specs with benefits.
            Format: Start each with "•"
            """;

        var response = await InvokeBedrockAsync(prompt, maxTokens: 300);

        return response
            .Split('\n')
            .Where(line => line.Trim().StartsWith("•"))
            .Select(line => line.Trim().TrimStart('•').Trim())
            .ToList();
    }

    public async Task<string> GenerateSEOMetaDescriptionAsync(Product product)
    {
        var prompt = $"""
            Write a compelling SEO meta description (150-160 characters) for this product page:

            Product: {product.Name}
            Category: {product.Category}
            Price: ₹{product.Price}

            Include: product name, key benefit, price range hint, and call-to-action.
            Target audience: Cricket players in India
            """;

        var response = await InvokeBedrockAsync(prompt, maxTokens: 100, temperature: 0.5);

        return response.Trim();
    }

    public async Task<Dictionary<string, string>> GenerateMultiLanguageDescriptionAsync(
        Product product,
        List<string> languages)
    {
        var descriptions = new Dictionary<string, string>();

        var englishDescription = await GenerateDescriptionAsync(product);
        descriptions["en"] = englishDescription.ShortDescription;

        foreach (var language in languages)
        {
            if (language == "en") continue;

            var prompt = $"""
                Translate this product description to {language}:

                {englishDescription.ShortDescription}

                Keep the tone professional and cricket-appropriate for {language} speakers in India.
                """;

            var translated = await InvokeBedrockAsync(prompt, maxTokens: 500);
            descriptions[language] = translated;
        }

        return descriptions;
    }

    private async Task<string> InvokeBedrockAsync(
        string prompt,
        int maxTokens = 1000,
        double temperature = 0.7)
    {
        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            ContentType = "application/json",
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = maxTokens,
                temperature = temperature,
                messages = new[]
                {
                    new { role = "user", content = prompt }
                }
            })))
        };

        var response = await _bedrock.InvokeModelAsync(request);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseBody);

        return result.Content[0].Text;
    }

    private string ExtractSection(string text, string sectionName)
    {
        var pattern = $@"{sectionName}:\s*(.+?)(?=\n\n[A-Z_]+:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private List<string> ExtractBulletPoints(string text)
    {
        var bulletSection = ExtractSection(text, "BULLET_POINTS");
        return bulletSection
            .Split('\n')
            .Where(line => line.Trim().StartsWith("•"))
            .Select(line => line.Trim().TrimStart('•').Trim())
            .ToList();
    }

    private List<string> ExtractKeywords(string text)
    {
        var keywordSection = ExtractSection(text, "SEO_KEYWORDS");
        return keywordSection
            .Split(new[] { ',', '\n' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(k => k.Trim())
            .ToList();
    }
}

public record GeneratedDescription
{
    public string ProductId { get; init; }
    public string LongDescription { get; init; }
    public string ShortDescription { get; init; }
    public List<string> BulletPoints { get; init; }
    public List<string> SEOKeywords { get; init; }
    public DateTime GeneratedAt { get; init; }
    public string ModelUsed { get; init; }
}

public record ClaudeResponse
{
    [JsonPropertyName("content")]
    public List<ContentBlock> Content { get; init; }

    [JsonPropertyName("stop_reason")]
    public string StopReason { get; init; }

    [JsonPropertyName("usage")]
    public UsageInfo Usage { get; init; }
}

public record ContentBlock
{
    [JsonPropertyName("type")]
    public string Type { get; init; }

    [JsonPropertyName("text")]
    public string Text { get; init; }
}

public record UsageInfo
{
    [JsonPropertyName("input_tokens")]
    public int InputTokens { get; init; }

    [JsonPropertyName("output_tokens")]
    public int OutputTokens { get; init; }
}
```

#### 2. API Controller

```csharp
// File: Gearify.CatalogService/API/Controllers/ProductDescriptionController.cs

[ApiController]
[Route("api/products/{productId}/description")]
public class ProductDescriptionController : ControllerBase
{
    private readonly IProductDescriptionService _descriptionService;
    private readonly IProductRepository _productRepository;

    [HttpPost("generate")]
    [Authorize(Roles = "Admin,ProductManager")]
    public async Task<ActionResult<GeneratedDescription>> GenerateDescription(
        string productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            return NotFound();

        var description = await _descriptionService.GenerateDescriptionAsync(product);

        return Ok(description);
    }

    [HttpPost("apply")]
    [Authorize(Roles = "Admin,ProductManager")]
    public async Task<ActionResult> ApplyGeneratedDescription(
        string productId,
        [FromBody] GeneratedDescription description)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            return NotFound();

        product.Description = description.LongDescription;
        product.ShortDescription = description.ShortDescription;
        product.BulletPoints = description.BulletPoints;
        product.SEOKeywords = description.SEOKeywords;

        await _productRepository.UpdateAsync(product);

        return Ok(new { message = "Product description updated successfully" });
    }

    [HttpGet("preview")]
    public async Task<ActionResult<GeneratedDescription>> PreviewDescription(string productId)
    {
        var product = await _productRepository.GetByIdAsync(productId);
        if (product == null)
            return NotFound();

        var description = await _descriptionService.GenerateDescriptionAsync(product);

        return Ok(description);
    }
}
```

#### 3. Admin UI Integration (Angular)

```typescript
// File: gearify-web/src/app/admin/components/product-description-generator.component.ts

import { Component, Input } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface GeneratedDescription {
  productId: string;
  longDescription: string;
  shortDescription: string;
  bulletPoints: string[];
  seoKeywords: string[];
  generatedAt: Date;
  modelUsed: string;
}

@Component({
  selector: 'app-product-description-generator',
  template: `
    <div class="description-generator">
      <h3>AI Description Generator</h3>

      <button
        (click)="generateDescription()"
        [disabled]="isGenerating"
        class="btn-primary">
        {{ isGenerating ? 'Generating...' : 'Generate with AI' }}
      </button>

      <div *ngIf="generatedDescription" class="preview">
        <h4>Preview</h4>

        <div class="section">
          <label>Long Description:</label>
          <textarea
            [(ngModel)]="generatedDescription.longDescription"
            rows="8"></textarea>
        </div>

        <div class="section">
          <label>Short Description:</label>
          <textarea
            [(ngModel)]="generatedDescription.shortDescription"
            rows="3"></textarea>
        </div>

        <div class="section">
          <label>Bullet Points:</label>
          <ul>
            <li *ngFor="let point of generatedDescription.bulletPoints; let i = index">
              <input [(ngModel)]="generatedDescription.bulletPoints[i]" />
            </li>
          </ul>
        </div>

        <div class="section">
          <label>SEO Keywords:</label>
          <input
            [value]="generatedDescription.seoKeywords.join(', ')"
            (change)="updateKeywords($event)" />
        </div>

        <div class="actions">
          <button (click)="applyDescription()" class="btn-success">
            Apply to Product
          </button>
          <button (click)="regenerate()" class="btn-secondary">
            Regenerate
          </button>
        </div>
      </div>
    </div>
  `
})
export class ProductDescriptionGeneratorComponent {
  @Input() productId: string;
  generatedDescription: GeneratedDescription | null = null;
  isGenerating = false;

  constructor(private http: HttpClient) {}

  async generateDescription() {
    this.isGenerating = true;

    try {
      this.generatedDescription = await this.http
        .post<GeneratedDescription>(
          `/api/products/${this.productId}/description/generate`,
          {}
        )
        .toPromise();
    } catch (error) {
      console.error('Failed to generate description:', error);
      alert('Failed to generate description. Please try again.');
    } finally {
      this.isGenerating = false;
    }
  }

  async applyDescription() {
    if (!this.generatedDescription) return;

    try {
      await this.http
        .post(
          `/api/products/${this.productId}/description/apply`,
          this.generatedDescription
        )
        .toPromise();

      alert('Description applied successfully!');
    } catch (error) {
      console.error('Failed to apply description:', error);
      alert('Failed to apply description. Please try again.');
    }
  }

  regenerate() {
    this.generatedDescription = null;
    this.generateDescription();
  }

  updateKeywords(event: any) {
    if (this.generatedDescription) {
      this.generatedDescription.seoKeywords = event.target.value
        .split(',')
        .map((k: string) => k.trim());
    }
  }
}
```

---

## Use Case 2: Intelligent Customer Support Chatbot

### Business Impact
- Handle 70-80% of customer queries automatically
- 24/7 availability
- Consistent, knowledgeable responses
- Escalate complex issues to humans
- Multi-language support

### Implementation

```csharp
// File: Gearify.NotificationService/Application/Services/BedrockChatbotService.cs

public interface IChatbotService
{
    Task<ChatbotResponse> HandleMessageAsync(string userId, string message, string sessionId);
    Task<List<ChatMessage>> GetConversationHistoryAsync(string sessionId);
}

public class BedrockChatbotService : IChatbotService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly IProductRepository _productRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IDistributedCache _cache;
    private readonly ILogger<BedrockChatbotService> _logger;

    private const string ModelId = "anthropic.claude-3-5-sonnet-20240620-v1:0";

    public async Task<ChatbotResponse> HandleMessageAsync(
        string userId,
        string message,
        string sessionId)
    {
        // Load conversation history
        var history = await GetConversationHistoryAsync(sessionId);

        // Detect intent and gather context
        var context = await GatherContextAsync(userId, message);

        // Build system prompt with company knowledge
        var systemPrompt = BuildSystemPrompt(context);

        // Prepare messages
        var messages = history
            .Select(m => new { role = m.Role, content = m.Content })
            .ToList();

        messages.Add(new { role = "user", content = message });

        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = 1500,
                temperature = 0.7,
                system = systemPrompt,
                messages = messages
            })))
        };

        var response = await _bedrock.InvokeModelAsync(request);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseBody);

        var assistantMessage = result.Content[0].Text;

        // Save conversation
        await SaveMessageAsync(sessionId, "user", message);
        await SaveMessageAsync(sessionId, "assistant", assistantMessage);

        // Check if escalation needed
        var needsEscalation = DetectEscalationNeed(assistantMessage);

        _logger.LogInformation(
            "Chatbot response generated for user {UserId}, session {SessionId}. Tokens: {InputTokens}/{OutputTokens}",
            userId, sessionId, result.Usage.InputTokens, result.Usage.OutputTokens);

        return new ChatbotResponse
        {
            Message = assistantMessage,
            SessionId = sessionId,
            NeedsHumanEscalation = needsEscalation,
            Timestamp = DateTime.UtcNow,
            TokensUsed = result.Usage.InputTokens + result.Usage.OutputTokens
        };
    }

    private string BuildSystemPrompt(ChatContext context)
    {
        var prompt = """
            You are a helpful customer service assistant for Gearify, India's premier online cricket equipment store.

            YOUR ROLE:
            - Help customers find the right cricket equipment
            - Answer product questions
            - Provide size and fit guidance
            - Track orders
            - Handle returns/exchanges
            - Recommend products based on skill level and playing style

            YOUR KNOWLEDGE:
            - Deep understanding of cricket (rules, formats, equipment)
            - Indian cricket context (IPL, Ranji Trophy, local tournaments)
            - Product specifications and differences
            - Size charts and fitting guidelines

            GUIDELINES:
            - Be friendly, professional, and enthusiastic about cricket
            - Ask clarifying questions when needed
            - Provide specific product recommendations with reasons
            - Include prices in Indian Rupees (₹)
            - If you don't know something, admit it and offer to escalate
            - Keep responses concise but informative (3-4 paragraphs max)

            ESCALATION:
            If you encounter any of these, say "Let me connect you with a human agent":
            - Complex technical issues
            - Payment disputes
            - Complaints requiring manager attention
            - Requests outside your knowledge
            """;

        // Add relevant product context if available
        if (context.RelevantProducts?.Any() == true)
        {
            prompt += "\n\nRELEVANT PRODUCTS:\n";
            foreach (var product in context.RelevantProducts.Take(5))
            {
                prompt += $"- {product.Name} ({product.Brand}, {product.Category}) - ₹{product.Price:N0}\n";
            }
        }

        // Add order context if available
        if (context.RecentOrders?.Any() == true)
        {
            prompt += "\n\nCUSTOMER'S RECENT ORDERS:\n";
            foreach (var order in context.RecentOrders.Take(3))
            {
                prompt += $"- Order #{order.Id}: {order.Status}, ₹{order.TotalAmount:N0}, {order.CreatedAt:dd MMM yyyy}\n";
            }
        }

        // Add user preferences
        if (context.UserPreferences != null)
        {
            prompt += $"\n\nCUSTOMER PREFERENCES:\n";
            prompt += $"- Preferred category: {context.UserPreferences.PreferredCategory}\n";
            prompt += $"- Budget range: ₹{context.UserPreferences.TypicalBudget}\n";
        }

        return prompt;
    }

    private async Task<ChatContext> GatherContextAsync(string userId, string message)
    {
        var context = new ChatContext();

        // Detect if user is asking about products
        if (IsProductQuery(message))
        {
            var searchTerms = ExtractSearchTerms(message);
            context.RelevantProducts = await _productRepository.SearchAsync(searchTerms, limit: 5);
        }

        // Detect if user is asking about orders
        if (IsOrderQuery(message))
        {
            context.RecentOrders = await _orderRepository.GetRecentByUserAsync(userId, limit: 3);
        }

        // Get user preferences
        context.UserPreferences = await GetUserPreferencesAsync(userId);

        return context;
    }

    private bool IsProductQuery(string message)
    {
        var productKeywords = new[]
        {
            "bat", "ball", "shoe", "helmet", "pad", "glove",
            "looking for", "need", "want", "buy", "recommend",
            "best", "good", "which", "compare"
        };

        return productKeywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private bool IsOrderQuery(string message)
    {
        var orderKeywords = new[]
        {
            "order", "delivery", "tracking", "shipped", "status",
            "where is my", "when will", "received"
        };

        return orderKeywords.Any(k => message.Contains(k, StringComparison.OrdinalIgnoreCase));
    }

    private string ExtractSearchTerms(string message)
    {
        // Simple keyword extraction (in production, use Bedrock or Comprehend for better extraction)
        return message;
    }

    private bool DetectEscalationNeed(string response)
    {
        var escalationPhrases = new[]
        {
            "connect you with a human",
            "escalate",
            "speak to a manager",
            "not sure",
            "don't know"
        };

        return escalationPhrases.Any(p => response.Contains(p, StringComparison.OrdinalIgnoreCase));
    }

    private async Task SaveMessageAsync(string sessionId, string role, string content)
    {
        var cacheKey = $"chat:history:{sessionId}";
        var history = await GetConversationHistoryAsync(sessionId);

        history.Add(new ChatMessage
        {
            Role = role,
            Content = content,
            Timestamp = DateTime.UtcNow
        });

        // Keep last 20 messages
        if (history.Count > 20)
        {
            history = history.TakeLast(20).ToList();
        }

        await _cache.SetStringAsync(
            cacheKey,
            JsonSerializer.Serialize(history),
            new DistributedCacheEntryOptions
            {
                AbsoluteExpirationRelativeToNow = TimeSpan.FromHours(24)
            });
    }

    public async Task<List<ChatMessage>> GetConversationHistoryAsync(string sessionId)
    {
        var cacheKey = $"chat:history:{sessionId}";
        var cached = await _cache.GetStringAsync(cacheKey);

        return cached != null
            ? JsonSerializer.Deserialize<List<ChatMessage>>(cached)
            : new List<ChatMessage>();
    }

    private async Task<UserPreferences> GetUserPreferencesAsync(string userId)
    {
        // Fetch from user profile or infer from purchase history
        return new UserPreferences
        {
            PreferredCategory = "Bats",
            TypicalBudget = 5000
        };
    }
}

public record ChatContext
{
    public List<Product> RelevantProducts { get; set; }
    public List<Order> RecentOrders { get; set; }
    public UserPreferences UserPreferences { get; set; }
}

public record ChatbotResponse
{
    public string Message { get; init; }
    public string SessionId { get; init; }
    public bool NeedsHumanEscalation { get; init; }
    public DateTime Timestamp { get; init; }
    public int TokensUsed { get; init; }
}

public record ChatMessage
{
    public string Role { get; init; } // "user" or "assistant"
    public string Content { get; init; }
    public DateTime Timestamp { get; init; }
}

public record UserPreferences
{
    public string PreferredCategory { get; init; }
    public decimal TypicalBudget { get; init; }
}
```

#### Real-time Chat API

```csharp
// File: Gearify.NotificationService/API/Controllers/ChatbotController.cs

[ApiController]
[Route("api/chatbot")]
public class ChatbotController : ControllerBase
{
    private readonly IChatbotService _chatbotService;

    [HttpPost("message")]
    public async Task<ActionResult<ChatbotResponse>> SendMessage(
        [FromBody] ChatMessageRequest request)
    {
        var userId = User.FindFirst("sub")?.Value ?? "anonymous";

        var response = await _chatbotService.HandleMessageAsync(
            userId,
            request.Message,
            request.SessionId ?? Guid.NewGuid().ToString()
        );

        return Ok(response);
    }

    [HttpGet("history/{sessionId}")]
    public async Task<ActionResult<List<ChatMessage>>> GetHistory(string sessionId)
    {
        var history = await _chatbotService.GetConversationHistoryAsync(sessionId);
        return Ok(history);
    }
}

public record ChatMessageRequest
{
    public string Message { get; init; }
    public string SessionId { get; init; }
}
```

#### Frontend Chat Widget (Angular)

```typescript
// File: gearify-web/src/app/components/chatbot-widget/chatbot-widget.component.ts

import { Component, OnInit } from '@angular/core';
import { HttpClient } from '@angular/common/http';

interface ChatMessage {
  role: 'user' | 'assistant';
  content: string;
  timestamp: Date;
}

@Component({
  selector: 'app-chatbot-widget',
  template: `
    <div class="chatbot-widget" [class.open]="isOpen">
      <div class="chat-header" (click)="toggle()">
        <span>🏏 Gearify Assistant</span>
        <button class="close-btn" *ngIf="isOpen">×</button>
      </div>

      <div class="chat-messages" *ngIf="isOpen" #messagesContainer>
        <div *ngFor="let msg of messages"
             [class]="'message ' + msg.role">
          <div class="content">{{ msg.content }}</div>
          <div class="timestamp">{{ msg.timestamp | date:'short' }}</div>
        </div>

        <div *ngIf="isTyping" class="message assistant typing">
          <span class="dots">●●●</span>
        </div>
      </div>

      <div class="chat-input" *ngIf="isOpen">
        <input
          [(ngModel)]="userInput"
          (keyup.enter)="sendMessage()"
          placeholder="Ask about cricket equipment..."
          [disabled]="isTyping" />
        <button (click)="sendMessage()" [disabled]="!userInput || isTyping">
          Send
        </button>
      </div>
    </div>
  `,
  styles: [`
    .chatbot-widget {
      position: fixed;
      bottom: 20px;
      right: 20px;
      width: 350px;
      background: white;
      border-radius: 10px;
      box-shadow: 0 4px 20px rgba(0,0,0,0.15);
      z-index: 1000;
    }

    .chat-header {
      background: #1976d2;
      color: white;
      padding: 15px;
      border-radius: 10px 10px 0 0;
      cursor: pointer;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }

    .chat-messages {
      height: 400px;
      overflow-y: auto;
      padding: 15px;
      background: #f5f5f5;
    }

    .message {
      margin-bottom: 15px;
      display: flex;
      flex-direction: column;
    }

    .message.user {
      align-items: flex-end;
    }

    .message.user .content {
      background: #1976d2;
      color: white;
    }

    .message.assistant .content {
      background: white;
      color: #333;
    }

    .content {
      padding: 10px 15px;
      border-radius: 15px;
      max-width: 80%;
      word-wrap: break-word;
    }

    .timestamp {
      font-size: 11px;
      color: #999;
      margin-top: 5px;
    }

    .typing .dots {
      animation: blink 1.4s infinite;
    }

    .chat-input {
      display: flex;
      padding: 10px;
      background: white;
      border-radius: 0 0 10px 10px;
    }

    .chat-input input {
      flex: 1;
      padding: 10px;
      border: 1px solid #ddd;
      border-radius: 5px;
      margin-right: 10px;
    }

    .chat-input button {
      padding: 10px 20px;
      background: #1976d2;
      color: white;
      border: none;
      border-radius: 5px;
      cursor: pointer;
    }

    @keyframes blink {
      0%, 100% { opacity: 1; }
      50% { opacity: 0.3; }
    }
  `]
})
export class ChatbotWidgetComponent implements OnInit {
  isOpen = false;
  messages: ChatMessage[] = [];
  userInput = '';
  isTyping = false;
  sessionId: string;

  constructor(private http: HttpClient) {
    this.sessionId = this.getOrCreateSessionId();
  }

  ngOnInit() {
    this.loadHistory();
  }

  toggle() {
    this.isOpen = !this.isOpen;
  }

  async sendMessage() {
    if (!this.userInput.trim()) return;

    const userMessage: ChatMessage = {
      role: 'user',
      content: this.userInput,
      timestamp: new Date()
    };

    this.messages.push(userMessage);
    this.userInput = '';
    this.isTyping = true;

    try {
      const response = await this.http.post<any>('/api/chatbot/message', {
        message: userMessage.content,
        sessionId: this.sessionId
      }).toPromise();

      this.messages.push({
        role: 'assistant',
        content: response.message,
        timestamp: new Date(response.timestamp)
      });

      if (response.needsHumanEscalation) {
        this.notifyHumanAgent();
      }
    } catch (error) {
      console.error('Chatbot error:', error);
      this.messages.push({
        role: 'assistant',
        content: 'Sorry, I encountered an error. Please try again.',
        timestamp: new Date()
      });
    } finally {
      this.isTyping = false;
      this.scrollToBottom();
    }
  }

  private async loadHistory() {
    try {
      const history = await this.http.get<ChatMessage[]>(
        `/api/chatbot/history/${this.sessionId}`
      ).toPromise();

      this.messages = history || [];
    } catch (error) {
      console.error('Failed to load chat history:', error);
    }
  }

  private getOrCreateSessionId(): string {
    let sessionId = sessionStorage.getItem('chatSessionId');
    if (!sessionId) {
      sessionId = `session-${Date.now()}-${Math.random().toString(36).substr(2, 9)}`;
      sessionStorage.setItem('chatSessionId', sessionId);
    }
    return sessionId;
  }

  private scrollToBottom() {
    setTimeout(() => {
      const container = document.querySelector('.chat-messages');
      if (container) {
        container.scrollTop = container.scrollHeight;
      }
    }, 100);
  }

  private notifyHumanAgent() {
    // Send notification to support team
    console.log('Human escalation requested for session:', this.sessionId);
  }
}
```

---

## Use Case 3: Review Summarization & Insights

### Implementation

```csharp
// File: Gearify.CatalogService/Application/Services/ReviewAnalysisService.cs

public interface IReviewAnalysisService
{
    Task<ReviewSummary> SummarizeReviewsAsync(string productId);
    Task<SentimentBreakdown> AnalyzeSentimentAsync(List<Review> reviews);
    Task<List<string>> ExtractKeyThemesAsync(List<Review> reviews);
}

public class ReviewAnalysisService : IReviewAnalysisService
{
    private readonly IAmazonBedrockRuntime _bedrock;
    private readonly IReviewRepository _reviewRepository;
    private const string ModelId = "anthropic.claude-3-haiku-20240307-v1:0"; // Cheaper for bulk analysis

    public async Task<ReviewSummary> SummarizeReviewsAsync(string productId)
    {
        var reviews = await _reviewRepository.GetByProductIdAsync(productId);

        if (!reviews.Any())
        {
            return new ReviewSummary
            {
                ProductId = productId,
                Summary = "No reviews yet",
                TotalReviews = 0
            };
        }

        var reviewTexts = string.Join("\n\n", reviews.Select((r, i) =>
            $"Review {i + 1} ({r.Rating}★): {r.Content}"));

        var prompt = $"""
            Analyze these customer reviews for a cricket product and provide:

            1. SUMMARY (3-4 sentences):
               - Overall customer satisfaction
               - Main themes and patterns
               - Key takeaways

            2. PROS (5-7 bullet points):
               - Most praised features
               - What customers love

            3. CONS (3-5 bullet points):
               - Common complaints
               - Areas for improvement

            4. KEY_THEMES:
               - 5-8 most mentioned topics/features

            5. BUYING_RECOMMENDATION:
               - Who should buy this product
               - Who should avoid it

            Reviews ({reviews.Count} total, avg rating: {reviews.Average(r => r.Rating):F1}★):
            {reviewTexts}

            Format as sections: SUMMARY:, PROS:, CONS:, KEY_THEMES:, BUYING_RECOMMENDATION:
            """;

        var response = await InvokeBedrockAsync(prompt, maxTokens: 1500);

        return new ReviewSummary
        {
            ProductId = productId,
            Summary = ExtractSection(response, "SUMMARY"),
            Pros = ExtractBulletPoints(response, "PROS"),
            Cons = ExtractBulletPoints(response, "CONS"),
            KeyThemes = ExtractList(response, "KEY_THEMES"),
            BuyingRecommendation = ExtractSection(response, "BUYING_RECOMMENDATION"),
            TotalReviews = reviews.Count,
            AverageRating = reviews.Average(r => r.Rating),
            GeneratedAt = DateTime.UtcNow
        };
    }

    public async Task<List<string>> ExtractKeyThemesAsync(List<Review> reviews)
    {
        var reviewTexts = string.Join("\n", reviews.Select(r => r.Content));

        var prompt = $"""
            Extract the 10 most frequently mentioned themes/topics from these product reviews.
            Return as a simple comma-separated list.

            Reviews:
            {reviewTexts}
            """;

        var response = await InvokeBedrockAsync(prompt, maxTokens: 200);

        return response
            .Split(',')
            .Select(t => t.Trim())
            .Where(t => !string.IsNullOrEmpty(t))
            .ToList();
    }

    private async Task<string> InvokeBedrockAsync(string prompt, int maxTokens = 1000)
    {
        var request = new InvokeModelRequest
        {
            ModelId = ModelId,
            Body = new MemoryStream(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new
            {
                anthropic_version = "bedrock-2023-05-31",
                max_tokens = maxTokens,
                temperature = 0.5,
                messages = new[] { new { role = "user", content = prompt } }
            })))
        };

        var response = await _bedrock.InvokeModelAsync(request);
        using var reader = new StreamReader(response.Body);
        var responseBody = await reader.ReadToEndAsync();
        var result = JsonSerializer.Deserialize<ClaudeResponse>(responseBody);

        return result.Content[0].Text;
    }

    private string ExtractSection(string text, string sectionName)
    {
        var pattern = $@"{sectionName}:\s*(.+?)(?=\n\n[A-Z_]+:|$)";
        var match = Regex.Match(text, pattern, RegexOptions.Singleline);
        return match.Success ? match.Groups[1].Value.Trim() : "";
    }

    private List<string> ExtractBulletPoints(string text, string sectionName)
    {
        var section = ExtractSection(text, sectionName);
        return section
            .Split('\n')
            .Where(line => line.Trim().StartsWith("•") || line.Trim().StartsWith("-"))
            .Select(line => line.Trim().TrimStart('•', '-').Trim())
            .ToList();
    }

    private List<string> ExtractList(string text, string sectionName)
    {
        var section = ExtractSection(text, sectionName);
        return section
            .Split(new[] { '\n', ',' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim().TrimStart('•', '-', '1', '2', '3', '4', '5', '6', '7', '8', '9', '0', '.').Trim())
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .ToList();
    }
}

public record ReviewSummary
{
    public string ProductId { get; init; }
    public string Summary { get; init; }
    public List<string> Pros { get; init; }
    public List<string> Cons { get; init; }
    public List<string> KeyThemes { get; init; }
    public string BuyingRecommendation { get; init; }
    public int TotalReviews { get; init; }
    public double AverageRating { get; init; }
    public DateTime GeneratedAt { get; init; }
}
```

---

## Cost Management

### Token Usage Optimization

```csharp
// File: Gearify.Shared/AI/BedrockCostTracker.cs

public class BedrockCostTracker
{
    private readonly ILogger<BedrockCostTracker> _logger;
    private readonly IAmazonCloudWatch _cloudWatch;

    private static readonly Dictionary<string, (decimal InputCost, decimal OutputCost)> ModelPricing = new()
    {
        ["anthropic.claude-3-5-sonnet-20240620-v1:0"] = (0.003m, 0.015m),
        ["anthropic.claude-3-haiku-20240307-v1:0"] = (0.00025m, 0.00125m),
        ["meta.llama3-70b-instruct-v1:0"] = (0.00099m, 0.00099m),
        ["amazon.titan-text-premier-v1:0"] = (0.0005m, 0.0015m)
    };

    public async Task TrackUsageAsync(
        string modelId,
        int inputTokens,
        int outputTokens,
        string feature)
    {
        if (!ModelPricing.ContainsKey(modelId))
        {
            _logger.LogWarning("Unknown model pricing for {ModelId}", modelId);
            return;
        }

        var pricing = ModelPricing[modelId];

        var inputCost = (inputTokens / 1000m) * pricing.InputCost;
        var outputCost = (outputTokens / 1000m) * pricing.OutputCost;
        var totalCost = inputCost + outputCost;

        _logger.LogInformation(
            "Bedrock usage: Model={Model}, Feature={Feature}, Tokens={Input}/{Output}, Cost=${Cost:F4}",
            modelId, feature, inputTokens, outputTokens, totalCost);

        // Send to CloudWatch for monitoring
        await _cloudWatch.PutMetricDataAsync(new PutMetricDataRequest
        {
            Namespace = "Gearify/Bedrock",
            MetricData = new List<MetricDatum>
            {
                new MetricDatum
                {
                    MetricName = "TokensUsed",
                    Value = inputTokens + outputTokens,
                    Unit = StandardUnit.Count,
                    Dimensions = new List<Dimension>
                    {
                        new Dimension { Name = "Model", Value = modelId },
                        new Dimension { Name = "Feature", Value = feature }
                    }
                },
                new MetricDatum
                {
                    MetricName = "Cost",
                    Value = (double)totalCost,
                    Unit = StandardUnit.None,
                    Dimensions = new List<Dimension>
                    {
                        new Dimension { Name = "Model", Value = modelId },
                        new Dimension { Name = "Feature", Value = feature }
                    }
                }
            }
        });
    }
}
```

### Monthly Cost Estimates

Based on 1000 products, 10,000 users, 500 chat sessions/day:

| Feature | Model | Monthly Usage | Estimated Cost |
|---------|-------|---------------|----------------|
| Product Descriptions | Claude 3.5 Sonnet | 1,000 products × 800 tokens | $12 |
| Chatbot | Claude 3.5 Sonnet | 15,000 conversations × 400 tokens | $180 |
| Review Summaries | Claude 3 Haiku | 500 products × 1000 tokens | $6 |
| Email Generation | Claude 3 Haiku | 5,000 emails × 300 tokens | $4 |
| **Total** | | | **~$200/month** |

---

## Deployment & Configuration

### appsettings.json

```json
{
  "AWS": {
    "Bedrock": {
      "Region": "us-east-1",
      "Models": {
        "Default": "anthropic.claude-3-5-sonnet-20240620-v1:0",
        "Fast": "anthropic.claude-3-haiku-20240307-v1:0",
        "CostEffective": "amazon.titan-text-premier-v1:0"
      },
      "Features": {
        "ProductDescriptions": {
          "Enabled": true,
          "Model": "anthropic.claude-3-5-sonnet-20240620-v1:0",
          "MaxTokens": 1000,
          "Temperature": 0.7,
          "CacheDuration": "7.00:00:00"
        },
        "Chatbot": {
          "Enabled": true,
          "Model": "anthropic.claude-3-5-sonnet-20240620-v1:0",
          "MaxTokens": 1500,
          "Temperature": 0.7,
          "SessionDuration": "24:00:00"
        },
        "ReviewAnalysis": {
          "Enabled": true,
          "Model": "anthropic.claude-3-haiku-20240307-v1:0",
          "MaxTokens": 1500,
          "Temperature": 0.5
        }
      },
      "RateLimits": {
        "RequestsPerMinute": 100,
        "TokensPerMinute": 50000
      }
    }
  }
}
```

### Startup.cs

```csharp
public void ConfigureServices(IServiceCollection services)
{
    // AWS Bedrock Runtime
    services.AddAWSService<IAmazonBedrockRuntime>();

    // Bedrock services
    services.AddScoped<IProductDescriptionService, ProductDescriptionService>();
    services.AddScoped<IChatbotService, BedrockChatbotService>();
    services.AddScoped<IReviewAnalysisService, ReviewAnalysisService>();

    // Cost tracking
    services.AddSingleton<BedrockCostTracker>();
}
```

---

## Testing

### Unit Tests with Mock

```csharp
[Fact]
public async Task GenerateDescription_ReturnsValidDescription()
{
    // Arrange
    var mockBedrock = new Mock<IAmazonBedrockRuntime>();
    mockBedrock
        .Setup(b => b.InvokeModelAsync(It.IsAny<InvokeModelRequest>(), default))
        .ReturnsAsync(CreateMockBedrockResponse("LONG_DESCRIPTION:\nTest description\n\nSHORT_DESCRIPTION:\nShort test"));

    var service = new ProductDescriptionService(
        mockBedrock.Object,
        Mock.Of<IDistributedCache>(),
        Mock.Of<ILogger<ProductDescriptionService>>()
    );

    var product = new Product
    {
        Id = "test-1",
        Name = "Test Bat",
        Category = "Bats",
        Brand = "SS"
    };

    // Act
    var result = await service.GenerateDescriptionAsync(product);

    // Assert
    Assert.NotNull(result);
    Assert.Contains("Test description", result.LongDescription);
}
```

---

## Monitoring & Alerts

### CloudWatch Alarms

```bash
# Alert if daily Bedrock costs exceed $50
aws cloudwatch put-metric-alarm \
  --alarm-name bedrock-daily-cost-high \
  --alarm-description "Bedrock daily costs exceeded $50" \
  --metric-name Cost \
  --namespace Gearify/Bedrock \
  --statistic Sum \
  --period 86400 \
  --threshold 50 \
  --comparison-operator GreaterThanThreshold \
  --evaluation-periods 1
```

---

## Best Practices

1. **Use Appropriate Models**:
   - Claude 3.5 Sonnet: Complex tasks, best quality
   - Claude 3 Haiku: Simple tasks, cost-effective
   - Titan: AWS-native, budget-friendly

2. **Cache Aggressively**:
   - Product descriptions: 7 days
   - Review summaries: 24 hours
   - Chatbot sessions: 24 hours

3. **Implement Rate Limiting**:
   - Prevent abuse
   - Control costs
   - Stay within AWS quotas

4. **Monitor Costs**:
   - Track token usage per feature
   - Set up CloudWatch alarms
   - Review monthly spending

5. **Graceful Degradation**:
   - Always have fallbacks
   - Handle errors gracefully
   - Don't break user experience

---

**Next**: See other feature documentation for additional AI capabilities.
