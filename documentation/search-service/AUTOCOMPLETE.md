# Autocomplete Feature Documentation

## Overview

The autocomplete feature provides real-time search suggestions as users type in the search box. It returns three types of suggestions:

1. **Brands** - Matching brand names (e.g., "SG", "Kookaburra")
2. **Categories** - Matching category names (e.g., "Gloves", "Bats")
3. **Products** - Matching product names (e.g., "SG Test Batting Gloves")

## API Endpoint

```
GET /api/search/autocomplete?prefix={text}&limit={n}
Header: X-Tenant-Id: {tenant}
```

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `prefix` | string | required | The search prefix (minimum 2 characters) |
| `limit` | int | 10 | Maximum suggestions to return (max: 20) |

### Response

```json
{
  "suggestions": [
    {
      "text": "SG",
      "type": "brand",
      "id": null,
      "slug": "sg"
    },
    {
      "text": "Gloves",
      "type": "category",
      "id": null,
      "slug": "gloves"
    },
    {
      "text": "SG Test Batting Gloves",
      "type": "product",
      "id": "18e5c43e-831a-4b9b-bb4b-bca3f1b19758",
      "slug": null
    }
  ]
}
```

### Suggestion Types

| Type | Description | Has ID | Has Slug |
|------|-------------|--------|----------|
| `brand` | Brand name suggestion | No | Yes |
| `category` | Category name suggestion | No | Yes |
| `product` | Product name suggestion | Yes | No |

---

## How It Works

### Architecture Flow

```
┌─────────────────┐     ┌──────────────────┐     ┌─────────────────┐
│   Frontend      │────▶│  SearchController │────▶│ AutocompleteQuery│
│   Search Box    │     │  /autocomplete    │     │    Handler      │
└─────────────────┘     └──────────────────┘     └─────────────────┘
                                                          │
                        ┌─────────────────────────────────┼─────────────────────────────────┐
                        │                                 │                                 │
                        ▼                                 ▼                                 ▼
              ┌─────────────────┐              ┌─────────────────┐              ┌─────────────────┐
              │ GetBrandSuggestions │          │GetCategorySuggestions│         │GetProductSuggestions│
              │   (limit: 3)    │              │   (limit: 3)    │              │   (limit: 10)   │
              └─────────────────┘              └─────────────────┘              └─────────────────┘
                        │                                 │                                 │
                        └─────────────────────────────────┼─────────────────────────────────┘
                                                          │
                                                          ▼
                                               ┌─────────────────────┐
                                               │  Combine & Dedupe   │
                                               │  Return top N       │
                                               └─────────────────────┘
```

### Step-by-Step Process

#### Step 1: User Types in Search Box

```
User types: "sg"
            ↓
Frontend debounces (200ms)
            ↓
API Call: GET /api/search/autocomplete?prefix=sg
```

#### Step 2: Handler Receives Request

```csharp
// AutocompleteQueryHandler.cs
public async Task<AutocompleteResponse> Handle(AutocompleteQuery request, ...)
{
    // Minimum 2 characters required
    if (request.Prefix.Length < 2)
        return new AutocompleteResponse(); // Empty

    var suggestions = new List<AutocompleteSuggestion>();

    // Fetch in priority order
    suggestions.AddRange(await GetBrandSuggestionsAsync(...));      // Priority 1
    suggestions.AddRange(await GetCategorySuggestionsAsync(...));   // Priority 2
    suggestions.AddRange(await GetProductSuggestionsAsync(...));    // Priority 3

    // Deduplicate and limit
    return suggestions.DistinctBy(s => (s.Text, s.Type)).Take(limit);
}
```

#### Step 3: Brand Suggestions Query

```json
// OpenSearch Query
{
  "size": 0,
  "query": {
    "bool": {
      "must": [
        { "term": { "isActive": true } },
        { "match": { "brand.autocomplete": "sg" } }
      ]
    }
  },
  "aggregations": {
    "brands": {
      "terms": { "field": "brand.keyword", "size": 3 }
    }
  }
}
```

**How it matches:**

```
Index Data:
┌────────────────────────────────────────┐
│ brand: "SG"      → brand.autocomplete: │
│                    ["s", "sg"]         │
│ brand: "SS"      → brand.autocomplete: │
│                    ["s", "ss"]         │
│ brand: "Spartan" → brand.autocomplete: │
│                    ["s", "sp", "spa"...│
└────────────────────────────────────────┘

Query "sg" matches:
  ✅ "SG" (exact match on "sg" token)
  ❌ "SS" (no "sg" token)
  ❌ "Spartan" (no "sg" token)
```

#### Step 4: Category Suggestions Query

```json
// OpenSearch Query
{
  "size": 0,
  "query": {
    "bool": {
      "must": [{ "term": { "isActive": true } }]
    }
  },
  "aggregations": {
    "categories": {
      "terms": { "field": "category.keyword", "size": 50 }
    }
  }
}
```

**Then filtered in memory:**

```csharp
categoriesBucket.Buckets
    .Where(b => b.Key.Contains(prefix, StringComparison.OrdinalIgnoreCase))
    .Take(limit)
```

**Example for prefix "glo":**

```
Aggregation Result:
┌─────────────────────────────┐
│ Bats (15)                   │ → "glo" not in "Bats" ❌
│ Gloves (12)                 │ → "glo" in "Gloves" ✅
│ Helmets (8)                 │ → "glo" not in "Helmets" ❌
│ Batting Gloves (6)          │ → "glo" in "Batting Gloves" ✅
└─────────────────────────────┘

Filtered Result: ["Gloves", "Batting Gloves"]
```

#### Step 5: Product Suggestions Query

```json
// OpenSearch Query
{
  "size": 10,
  "_source": ["id", "name", "brand", "category"],
  "query": {
    "bool": {
      "must": [
        { "term": { "isActive": true } },
        {
          "multi_match": {
            "query": "sg",
            "fields": ["name.autocomplete^3", "brand.autocomplete^2"],
            "type": "bool_prefix"
          }
        }
      ]
    }
  }
}
```

**Field boosting:**

| Field | Boost | Meaning |
|-------|-------|---------|
| `name.autocomplete` | 3x | Product name matches rank highest |
| `brand.autocomplete` | 2x | Brand matches rank second |

---

## Index Mapping

### Edge N-Gram Analyzer

The autocomplete feature uses edge n-gram tokenization to enable prefix matching:

```json
{
  "settings": {
    "analysis": {
      "analyzer": {
        "autocomplete_index": {
          "tokenizer": "standard",
          "filter": ["lowercase", "asciifolding", "autocomplete_filter"]
        },
        "autocomplete_search": {
          "tokenizer": "standard",
          "filter": ["lowercase", "asciifolding"]
        }
      },
      "filter": {
        "autocomplete_filter": {
          "type": "edge_ngram",
          "min_gram": 1,
          "max_gram": 20
        }
      }
    }
  }
}
```

### How Edge N-Gram Works

```
Input: "Kookaburra"

Tokenization (autocomplete_index analyzer):
┌──────────────────────────────────────────────────────────┐
│ Standard Tokenizer: "kookaburra" (lowercased)            │
│                                                          │
│ Edge N-Gram Filter (min:1, max:20):                      │
│ → "k"                                                    │
│ → "ko"                                                   │
│ → "koo"                                                  │
│ → "kook"                                                 │
│ → "kooka"                                                │
│ → "kookab"                                               │
│ → "kookabu"                                              │
│ → "kookabur"                                             │
│ → "kookaburr"                                            │
│ → "kookaburra"                                           │
└──────────────────────────────────────────────────────────┘

Search Query: "kook"

Tokenization (autocomplete_search analyzer):
┌──────────────────────────────────────────────────────────┐
│ Standard Tokenizer: "kook" (lowercased)                  │
│ (No edge n-gram - searches exact token)                  │
└──────────────────────────────────────────────────────────┘

Match: "kook" matches indexed token "kook" ✅
```

### Field Mappings

```json
{
  "mappings": {
    "properties": {
      "name": {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword" },
          "autocomplete": {
            "type": "text",
            "analyzer": "autocomplete_index",
            "search_analyzer": "autocomplete_search"
          }
        }
      },
      "brand": {
        "type": "text",
        "fields": {
          "keyword": { "type": "keyword" },
          "autocomplete": {
            "type": "text",
            "analyzer": "autocomplete_index",
            "search_analyzer": "autocomplete_search"
          }
        }
      }
    }
  }
}
```

---

## Examples

### Example 1: Brand Search

**Request:**
```bash
curl "http://localhost:5012/api/search/autocomplete?prefix=kook" \
  -H "X-Tenant-Id: default"
```

**Response:**
```json
{
  "suggestions": [
    {"text": "Kookaburra", "type": "brand", "slug": "kookaburra"},
    {"text": "Kookaburra Turf Ball - White", "type": "product", "id": "12713f8e-..."},
    {"text": "Kookaburra Kahuna Cricket Helmet", "type": "product", "id": "18e29e9f-..."},
    {"text": "Kookaburra Kahuna Batting Gloves", "type": "product", "id": "1a7d2605-..."}
  ]
}
```

### Example 2: Category Search

**Request:**
```bash
curl "http://localhost:5012/api/search/autocomplete?prefix=hel" \
  -H "X-Tenant-Id: default"
```

**Response:**
```json
{
  "suggestions": [
    {"text": "Helmets", "type": "category", "slug": "helmets"},
    {"text": "SS Master Cricket Helmet", "type": "product", "id": "13601e8c-..."},
    {"text": "Kookaburra Kahuna Cricket Helmet", "type": "product", "id": "18e29e9f-..."},
    {"text": "SG Aerogym Cricket Helmet with Titanium Grill", "type": "product", "id": "862cef10-..."}
  ]
}
```

### Example 3: Product Search

**Request:**
```bash
curl "http://localhost:5012/api/search/autocomplete?prefix=bat" \
  -H "X-Tenant-Id: default"
```

**Response:**
```json
{
  "suggestions": [
    {"text": "Bats", "type": "category", "slug": "bats"},
    {"text": "SS Ton Reserve Edition English Willow Cricket Bat", "type": "product", "id": "022f46ce-..."},
    {"text": "DSC Pearla Stroke Kashmir Willow Bat", "type": "product", "id": "116c11d6-..."},
    {"text": "MRF Legend VK18 English Willow Bat", "type": "product", "id": "1a53a237-..."}
  ]
}
```

### Example 4: Short Prefix (Rejected)

**Request:**
```bash
curl "http://localhost:5012/api/search/autocomplete?prefix=a" \
  -H "X-Tenant-Id: default"
```

**Response:**
```json
{
  "suggestions": []
}
```

*Note: Minimum 2 characters required to prevent too many results.*

---

## Frontend Integration

### React Example

```tsx
import { useState, useEffect, useCallback } from 'react';
import { debounce } from 'lodash';

interface Suggestion {
  text: string;
  type: 'brand' | 'category' | 'product';
  id?: string;
  slug?: string;
}

const SearchBox = () => {
  const [query, setQuery] = useState('');
  const [suggestions, setSuggestions] = useState<Suggestion[]>([]);
  const [isOpen, setIsOpen] = useState(false);

  // Debounced autocomplete fetch
  const fetchSuggestions = useCallback(
    debounce(async (prefix: string) => {
      if (prefix.length < 2) {
        setSuggestions([]);
        return;
      }

      const response = await fetch(
        `/api/search/autocomplete?prefix=${encodeURIComponent(prefix)}`,
        { headers: { 'X-Tenant-Id': 'default' } }
      );
      const data = await response.json();
      setSuggestions(data.suggestions);
      setIsOpen(true);
    }, 200),
    []
  );

  useEffect(() => {
    fetchSuggestions(query);
  }, [query, fetchSuggestions]);

  // Handle suggestion click
  const handleSuggestionClick = (suggestion: Suggestion) => {
    switch (suggestion.type) {
      case 'product':
        // Navigate to product detail page
        window.location.href = `/products/${suggestion.id}`;
        break;
      case 'brand':
        // Navigate to search with brand filter
        window.location.href = `/search?brand=${suggestion.slug}`;
        break;
      case 'category':
        // Navigate to search with category filter
        window.location.href = `/search?category=${suggestion.slug}`;
        break;
    }
    setIsOpen(false);
  };

  // Handle form submit (full search)
  const handleSubmit = (e: React.FormEvent) => {
    e.preventDefault();
    window.location.href = `/search?q=${encodeURIComponent(query)}`;
  };

  return (
    <form onSubmit={handleSubmit} className="search-box">
      <input
        type="text"
        value={query}
        onChange={(e) => setQuery(e.target.value)}
        onFocus={() => suggestions.length > 0 && setIsOpen(true)}
        placeholder="Search products..."
      />

      {isOpen && suggestions.length > 0 && (
        <ul className="suggestions-dropdown">
          {suggestions.map((suggestion, index) => (
            <li
              key={`${suggestion.type}-${suggestion.text}-${index}`}
              onClick={() => handleSuggestionClick(suggestion)}
              className={`suggestion-item suggestion-${suggestion.type}`}
            >
              <span className="suggestion-type">{suggestion.type}</span>
              <span className="suggestion-text">{suggestion.text}</span>
            </li>
          ))}
        </ul>
      )}
    </form>
  );
};
```

### CSS Styling Example

```css
.search-box {
  position: relative;
  width: 400px;
}

.suggestions-dropdown {
  position: absolute;
  top: 100%;
  left: 0;
  right: 0;
  background: white;
  border: 1px solid #ddd;
  border-radius: 4px;
  box-shadow: 0 4px 6px rgba(0, 0, 0, 0.1);
  max-height: 400px;
  overflow-y: auto;
  z-index: 1000;
}

.suggestion-item {
  padding: 10px 15px;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 10px;
}

.suggestion-item:hover {
  background: #f5f5f5;
}

.suggestion-type {
  font-size: 10px;
  text-transform: uppercase;
  padding: 2px 6px;
  border-radius: 3px;
  font-weight: 600;
}

.suggestion-brand .suggestion-type {
  background: #e3f2fd;
  color: #1976d2;
}

.suggestion-category .suggestion-type {
  background: #f3e5f5;
  color: #7b1fa2;
}

.suggestion-product .suggestion-type {
  background: #e8f5e9;
  color: #388e3c;
}
```

---

## Autocomplete vs Full Search

| Aspect | Autocomplete | Full Search |
|--------|--------------|-------------|
| **When Called** | While user types (debounced) | On Enter/Submit |
| **Response Time** | ~50-100ms | ~200-500ms |
| **Data Returned** | Name, type, ID only | Full product details |
| **Facets** | No | Yes |
| **Pagination** | No (max 20 items) | Yes |
| **Filters** | No | Yes |
| **Use Case** | Quick suggestions dropdown | Full results page |

### Decision Flow

```
User interaction with search box
              │
              ▼
┌─────────────────────────────────┐
│ Is user still typing?           │
│ (check after 200ms debounce)    │
└─────────────────────────────────┘
              │
     ┌────────┴────────┐
     │ Yes             │ No (Enter/Click)
     ▼                 ▼
┌──────────────┐  ┌──────────────────┐
│ Autocomplete │  │ Full Search      │
│ API Call     │  │ API Call         │
│              │  │                  │
│ Show dropdown│  │ Navigate to      │
│ suggestions  │  │ results page     │
└──────────────┘  └──────────────────┘
```

---

## Performance Considerations

### 1. Debouncing

Always debounce autocomplete requests (150-300ms) to avoid excessive API calls:

```typescript
// Without debounce: "kookaburra" = 10 API calls
// With 200ms debounce: "kookaburra" = 1-2 API calls
```

### 2. Minimum Character Limit

The API requires minimum 2 characters to prevent returning too many results:

```csharp
if (request.Prefix.Length < 2)
    return new AutocompleteResponse(); // Empty
```

### 3. Result Limits

- Brands: max 3 suggestions
- Categories: max 3 suggestions
- Products: max 10 suggestions
- Total: capped at `limit` parameter (max 20)

### 4. Query Optimization

- Uses `_source` filtering to return only needed fields
- Uses aggregations for brands/categories (no document fetching)
- Separate analyzers for index vs search (edge n-gram only at index time)

---

## Troubleshooting

### Empty Results

1. **Check minimum length**: Prefix must be at least 2 characters
2. **Check tenant ID**: Ensure correct `X-Tenant-Id` header
3. **Check index exists**: Run `/api/admin/index/{tenantId}/products/exists`
4. **Check products are indexed**: Run a full search to verify data exists

### Slow Response

1. **Check debouncing**: Frontend should debounce 150-300ms
2. **Check network**: Verify no network latency issues
3. **Check index health**: OpenSearch cluster may be overloaded

### Missing Suggestions

1. **Brand not appearing**: Ensure brand field is populated in product data
2. **Category not appearing**: Ensure category field is populated
3. **Product not appearing**: Check if `isActive: true` for the product
