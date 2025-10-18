# Load Balancing & Auto-Scaling Guide for Gearify

Complete guide on how to handle load balancing when services automatically scale up/down.

---

## Table of Contents
- [The Problem](#the-problem)
- [Current Setup (Static Configuration)](#current-setup-static-configuration)
- [Solution 1: Kubernetes + Service Discovery](#solution-1-kubernetes--service-discovery)
- [Solution 2: AWS ECS + Application Load Balancer](#solution-2-aws-ecs--application-load-balancer)
- [Solution 3: Consul + YARP Dynamic Configuration](#solution-3-consul--yarp-dynamic-configuration)
- [Solution 4: Docker Swarm](#solution-4-docker-swarm)
- [Recommended Architecture](#recommended-architecture)
- [Implementation Examples](#implementation-examples)

---

## The Problem

### Current Setup (Static Configuration)

Right now, your API Gateway has **hardcoded** service addresses:

```json
{
  "Clusters": {
    "catalog-cluster": {
      "Destinations": {
        "catalog-destination": {
          "Address": "http://catalog-svc:80"  // ← Only ONE instance
        }
      }
    }
  }
}
```

**Problems with this:**
1. ❌ Only points to ONE instance
2. ❌ If you scale to 3 instances, gateway still only knows about 1
3. ❌ Manual updates needed when scaling
4. ❌ No automatic failover
5. ❌ No health checking

### What Happens When You Auto-Scale?

**Scenario:** Catalog service scales from 1 → 3 instances

```
┌─────────────────┐
│  API Gateway    │
│  (Port 5000)    │
└────────┬────────┘
         │
         ↓ (Only knows about one address)
    catalog-svc:80
         ↓
    Which instance gets the traffic? 🤔
```

**The instances:**
```
catalog-svc-1 (10.0.1.10:80)  ← Only this one gets traffic
catalog-svc-2 (10.0.1.11:80)  ← Sitting idle
catalog-svc-3 (10.0.1.12:80)  ← Sitting idle
```

---

## Solution 1: Kubernetes + Service Discovery (RECOMMENDED)

Kubernetes has **built-in service discovery and load balancing**.

### How It Works

```
┌─────────────────────────────────────────────────┐
│               Kubernetes Cluster                │
│                                                 │
│  ┌───────────────┐                             │
│  │  API Gateway  │                             │
│  │   (Pod)       │                             │
│  └───────┬───────┘                             │
│          │                                      │
│          ↓                                      │
│  ┌───────────────────┐                         │
│  │ Kubernetes Service│ ← DNS: catalog-svc      │
│  │   (Load Balancer) │                         │
│  └─────────┬─────────┘                         │
│            │                                    │
│    ┌───────┴───────┬──────────┐                │
│    ↓               ↓          ↓                │
│  ┌─────┐      ┌─────┐     ┌─────┐             │
│  │Pod 1│      │Pod 2│     │Pod 3│             │
│  │5001 │      │5001 │     │5001 │             │
│  └─────┘      └─────┘     └─────┘             │
│  Catalog-1    Catalog-2   Catalog-3            │
└─────────────────────────────────────────────────┘
```

### Configuration

**1. Kubernetes Service Definition** (`catalog-service.yaml`):

```yaml
apiVersion: v1
kind: Service
metadata:
  name: catalog-svc
spec:
  selector:
    app: catalog  # Matches all pods with this label
  ports:
    - protocol: TCP
      port: 80
      targetPort: 5001
  type: ClusterIP  # Internal load balancer
```

**2. Deployment with Auto-Scaling** (`catalog-deployment.yaml`):

```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: catalog-deployment
spec:
  replicas: 3  # Start with 3 instances
  selector:
    matchLabels:
      app: catalog
  template:
    metadata:
      labels:
        app: catalog
    spec:
      containers:
      - name: catalog
        image: gearify/catalog-svc:latest
        ports:
        - containerPort: 5001
        resources:
          requests:
            cpu: "100m"
            memory: "128Mi"
          limits:
            cpu: "500m"
            memory: "512Mi"
---
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: catalog-hpa
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: catalog-deployment
  minReplicas: 2      # Minimum instances
  maxReplicas: 10     # Maximum instances
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70  # Scale when CPU > 70%
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80  # Scale when memory > 80%
```

**3. API Gateway Configuration** (SAME as before!):

```json
{
  "Clusters": {
    "catalog-cluster": {
      "Destinations": {
        "catalog-destination": {
          "Address": "http://catalog-svc:80"  // ← Kubernetes Service name
        }
      }
    }
  }
}
```

### What Happens Automatically

**When load increases:**

1. **CPU/Memory hits threshold** (e.g., 70% CPU)
2. **HPA detects** and creates new pods
3. **Kubernetes Service** automatically adds new pod IPs to internal load balancer
4. **Traffic distributes** across all healthy pods
5. **No config changes needed!** ✅

**When load decreases:**

1. **CPU/Memory drops** below threshold
2. **HPA scales down** (removes pods)
3. **Kubernetes Service** removes terminated pod IPs
4. **Traffic redistributes** to remaining pods

### Load Balancing Algorithm

Kubernetes uses **round-robin** by default:

```
Request 1 → Pod 1
Request 2 → Pod 2
Request 3 → Pod 3
Request 4 → Pod 1
Request 5 → Pod 2
...
```

### Health Checks

**Add liveness and readiness probes:**

```yaml
spec:
  containers:
  - name: catalog
    livenessProbe:
      httpGet:
        path: /health
        port: 5001
      initialDelaySeconds: 30
      periodSeconds: 10
    readinessProbe:
      httpGet:
        path: /health/ready
        port: 5001
      initialDelaySeconds: 5
      periodSeconds: 5
```

**What this does:**
- **Liveness**: Restarts pod if unhealthy
- **Readiness**: Removes from load balancer if not ready (still starting, deploying, etc.)

---

## Solution 2: AWS ECS + Application Load Balancer

If you're using **AWS ECS** (Elastic Container Service).

### Architecture

```
                    ┌────────────────────┐
                    │   Route 53 (DNS)   │
                    │  api.gearify.com   │
                    └──────────┬─────────┘
                               ↓
                    ┌────────────────────┐
                    │  CloudFront (CDN)  │
                    └──────────┬─────────┘
                               ↓
┌──────────────────────────────────────────────────────────┐
│                         AWS VPC                          │
│                                                          │
│  ┌────────────────────────────────────────────────┐     │
│  │  Application Load Balancer (ALB)               │     │
│  │  Target Group: catalog-service                 │     │
│  └────────────┬───────────────────────────────────┘     │
│               │                                          │
│       ┌───────┴────────┬──────────┐                     │
│       ↓                ↓          ↓                     │
│  ┌─────────┐     ┌─────────┐  ┌─────────┐              │
│  │ECS Task │     │ECS Task │  │ECS Task │              │
│  │catalog-1│     │catalog-2│  │catalog-3│              │
│  └─────────┘     └─────────┘  └─────────┘              │
│                                                          │
│  ┌────────────────────────────────────────────────┐     │
│  │  ECS Service with Auto Scaling                 │     │
│  │  Min: 2, Max: 10, Target CPU: 70%             │     │
│  └────────────────────────────────────────────────┘     │
└──────────────────────────────────────────────────────────┘
```

### ECS Service Definition (Terraform)

```hcl
# ECS Service with Auto Scaling
resource "aws_ecs_service" "catalog" {
  name            = "catalog-service"
  cluster         = aws_ecs_cluster.gearify.id
  task_definition = aws_ecs_task_definition.catalog.arn
  desired_count   = 3

  load_balancer {
    target_group_arn = aws_lb_target_group.catalog.arn
    container_name   = "catalog"
    container_port   = 5001
  }

  network_configuration {
    subnets         = aws_subnet.private[*].id
    security_groups = [aws_security_group.catalog.id]
  }
}

# Auto Scaling
resource "aws_appautoscaling_target" "catalog" {
  max_capacity       = 10
  min_capacity       = 2
  resource_id        = "service/${aws_ecs_cluster.gearify.name}/${aws_ecs_service.catalog.name}"
  scalable_dimension = "ecs:service:DesiredCount"
  service_namespace  = "ecs"
}

# Scale up when CPU > 70%
resource "aws_appautoscaling_policy" "catalog_cpu" {
  name               = "catalog-cpu-scaling"
  policy_type        = "TargetTrackingScaling"
  resource_id        = aws_appautoscaling_target.catalog.resource_id
  scalable_dimension = aws_appautoscaling_target.catalog.scalable_dimension
  service_namespace  = aws_appautoscaling_target.catalog.service_namespace

  target_tracking_scaling_policy_configuration {
    predefined_metric_specification {
      predefined_metric_type = "ECSServiceAverageCPUUtilization"
    }
    target_value = 70.0
  }
}

# Application Load Balancer
resource "aws_lb" "api_gateway" {
  name               = "gearify-alb"
  internal           = false
  load_balancer_type = "application"
  security_groups    = [aws_security_group.alb.id]
  subnets            = aws_subnet.public[*].id
}

# Target Group for Catalog Service
resource "aws_lb_target_group" "catalog" {
  name     = "catalog-tg"
  port     = 80
  protocol = "HTTP"
  vpc_id   = aws_vpc.main.id

  health_check {
    path                = "/health"
    healthy_threshold   = 2
    unhealthy_threshold = 3
    timeout             = 5
    interval            = 30
  }
}

# Listener Rule
resource "aws_lb_listener_rule" "catalog" {
  listener_arn = aws_lb_listener.http.arn
  priority     = 100

  action {
    type             = "forward"
    target_group_arn = aws_lb_target_group.catalog.arn
  }

  condition {
    path_pattern {
      values = ["/api/catalog/*"]
    }
  }
}
```

### How It Works

1. **ALB receives request** → `/api/catalog/products`
2. **ALB checks target group** → finds all healthy ECS tasks
3. **ALB distributes** using round-robin (or least outstanding requests)
4. **ECS Auto Scaling** monitors CPU/memory
5. **Scales tasks** up/down automatically
6. **ALB discovers** new tasks via service registry
7. **No manual config changes!**

---

## Solution 3: Consul + YARP Dynamic Configuration

Use **HashiCorp Consul** for service discovery with YARP.

### Architecture

```
┌────────────────┐
│  API Gateway   │
└────────┬───────┘
         │
         ↓ (Queries Consul)
┌────────────────────┐
│  Consul Server     │ ← Service Registry
│  (Service Catalog) │
└────────┬───────────┘
         │
    ┌────┴────┬────────┐
    ↓         ↓        ↓
  Catalog-1  Catalog-2  Catalog-3
  (Registers) (Registers) (Registers)
```

### Implementation

**1. Install Consul NuGet package:**

```bash
dotnet add package Consul
dotnet add package Yarp.ReverseProxy
```

**2. Create Dynamic Configuration Provider:**

```csharp
// Infrastructure/ServiceDiscovery/ConsulServiceDiscovery.cs
using Consul;
using Yarp.ReverseProxy.Configuration;

public class ConsulProxyConfigProvider : IProxyConfigProvider
{
    private readonly IConsulClient _consulClient;
    private readonly ILogger<ConsulProxyConfigProvider> _logger;
    private ConsulProxyConfig _config;

    public ConsulProxyConfigProvider(
        IConsulClient consulClient,
        ILogger<ConsulProxyConfigProvider> logger)
    {
        _consulClient = consulClient;
        _logger = logger;

        // Initial load
        _config = LoadConfigFromConsul().GetAwaiter().GetResult();

        // Watch for changes
        _ = WatchConsulChanges();
    }

    public IProxyConfig GetConfig() => _config;

    private async Task<ConsulProxyConfig> LoadConfigFromConsul()
    {
        var routes = new List<RouteConfig>();
        var clusters = new Dictionary<string, ClusterConfig>();

        // Query Consul for all services
        var services = await _consulClient.Catalog.Services();

        foreach (var service in services.Response)
        {
            var serviceName = service.Key;

            // Get healthy instances
            var healthyServices = await _consulClient.Health.Service(
                serviceName,
                "",
                true); // Only healthy

            if (!healthyServices.Response.Any())
                continue;

            // Create destinations from healthy instances
            var destinations = new Dictionary<string, DestinationConfig>();

            foreach (var instance in healthyServices.Response)
            {
                var address = instance.Service.Address;
                var port = instance.Service.Port;

                destinations.Add(
                    $"{serviceName}-{instance.Service.ID}",
                    new DestinationConfig
                    {
                        Address = $"http://{address}:{port}"
                    });
            }

            // Create cluster
            clusters.Add(
                $"{serviceName}-cluster",
                new ClusterConfig
                {
                    ClusterId = $"{serviceName}-cluster",
                    Destinations = destinations,
                    LoadBalancingPolicy = "RoundRobin"
                });

            // Create route
            routes.Add(new RouteConfig
            {
                RouteId = $"{serviceName}-route",
                ClusterId = $"{serviceName}-cluster",
                Match = new RouteMatch
                {
                    Path = $"/api/{serviceName}/{{**catch-all}}"
                }
            });
        }

        _logger.LogInformation(
            "Loaded {RouteCount} routes and {ClusterCount} clusters from Consul",
            routes.Count,
            clusters.Count);

        return new ConsulProxyConfig(routes, clusters);
    }

    private async Task WatchConsulChanges()
    {
        while (true)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(30));

                var newConfig = await LoadConfigFromConsul();
                var oldConfig = _config;

                _config = newConfig;

                // Notify YARP of config change
                oldConfig.SignalChange();

                _logger.LogInformation("Configuration updated from Consul");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error watching Consul changes");
            }
        }
    }
}

public class ConsulProxyConfig : IProxyConfig
{
    private readonly CancellationTokenSource _cts = new();

    public ConsulProxyConfig(
        IReadOnlyList<RouteConfig> routes,
        IReadOnlyDictionary<string, ClusterConfig> clusters)
    {
        Routes = routes;
        Clusters = clusters;
        ChangeToken = new CancellationChangeToken(_cts.Token);
    }

    public IReadOnlyList<RouteConfig> Routes { get; }
    public IReadOnlyList<ClusterConfig> Clusters { get; }
    public IChangeToken ChangeToken { get; }

    internal void SignalChange()
    {
        _cts.Cancel();
    }
}
```

**3. Register services in Consul (from each microservice):**

```csharp
// In each microservice's Program.cs
public static async Task Main(string[] args)
{
    var builder = WebApplication.CreateBuilder(args);

    // ... other services

    builder.Services.AddSingleton<IConsulClient>(p =>
        new ConsulClient(config =>
        {
            config.Address = new Uri("http://consul:8500");
        }));

    var app = builder.Build();

    // Register with Consul on startup
    var consulClient = app.Services.GetRequiredService<IConsulClient>();
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();

    var serviceId = $"catalog-{Guid.NewGuid()}";
    var registration = new AgentServiceRegistration
    {
        ID = serviceId,
        Name = "catalog",
        Address = "catalog-svc",
        Port = 5001,
        Check = new AgentServiceCheck
        {
            HTTP = "http://catalog-svc:5001/health",
            Interval = TimeSpan.FromSeconds(10),
            Timeout = TimeSpan.FromSeconds(5)
        }
    };

    await consulClient.Agent.ServiceRegister(registration);

    // Deregister on shutdown
    lifetime.ApplicationStopping.Register(async () =>
    {
        await consulClient.Agent.ServiceDeregister(serviceId);
    });

    await app.RunAsync();
}
```

**4. Configure API Gateway to use Consul:**

```csharp
// API Gateway Program.cs
builder.Services.AddSingleton<IConsulClient>(p =>
    new ConsulClient(config =>
    {
        config.Address = new Uri("http://consul:8500");
    }));

builder.Services.AddSingleton<IProxyConfigProvider, ConsulProxyConfigProvider>();

builder.Services.AddReverseProxy();
```

### What Happens

1. **Services start** → Register with Consul
2. **Consul tracks** all healthy instances
3. **API Gateway queries** Consul every 30 seconds
4. **New instance added?** → Automatically discovered
5. **Instance dies?** → Removed from load balancing
6. **Zero configuration changes!**

---

## Solution 4: Docker Swarm

If using **Docker Swarm** mode.

### Configuration

```yaml
# docker-compose.yml
version: '3.8'

services:
  catalog-svc:
    image: gearify/catalog-svc:latest
    deploy:
      replicas: 3  # Start with 3 instances
      update_config:
        parallelism: 1
        delay: 10s
      restart_policy:
        condition: on-failure
      resources:
        limits:
          cpus: '0.5'
          memory: 512M
        reservations:
          cpus: '0.25'
          memory: 256M
    networks:
      - gearify-network

  api-gateway:
    image: gearify/api-gateway:latest
    ports:
      - "5000:80"
    environment:
      - CATALOG_SERVICE_URL=http://catalog-svc:80  # Swarm resolves this
    networks:
      - gearify-network

networks:
  gearify-network:
    driver: overlay
```

**Docker Swarm handles load balancing automatically using internal DNS!**

### Auto-Scaling with Swarm

```bash
# Scale up
docker service scale gearify_catalog-svc=5

# Scale down
docker service scale gearify_catalog-svc=2

# Auto-scale based on CPU (requires additional tools like Orbiter)
```

---

## Recommended Architecture for Gearify

For production, I recommend **Kubernetes** with this setup:

### Complete Kubernetes Example

```yaml
# namespace.yaml
apiVersion: v1
kind: Namespace
metadata:
  name: gearify

---
# catalog-service.yaml
apiVersion: v1
kind: Service
metadata:
  name: catalog-svc
  namespace: gearify
spec:
  selector:
    app: catalog
  ports:
    - port: 80
      targetPort: 5001
  type: ClusterIP

---
# catalog-deployment.yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: catalog
  namespace: gearify
spec:
  replicas: 3
  selector:
    matchLabels:
      app: catalog
  template:
    metadata:
      labels:
        app: catalog
        version: v1
    spec:
      containers:
      - name: catalog
        image: gearify/catalog-svc:latest
        ports:
        - containerPort: 5001
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: DYNAMODB_ENDPOINT
          value: "http://dynamodb:8000"
        livenessProbe:
          httpGet:
            path: /health
            port: 5001
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health/ready
            port: 5001
          initialDelaySeconds: 5
          periodSeconds: 5
        resources:
          requests:
            cpu: "100m"
            memory: "128Mi"
          limits:
            cpu: "500m"
            memory: "512Mi"

---
# catalog-hpa.yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: catalog-hpa
  namespace: gearify
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: catalog
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
  behavior:
    scaleDown:
      stabilizationWindowSeconds: 300  # Wait 5 min before scaling down
      policies:
      - type: Percent
        value: 50
        periodSeconds: 60
    scaleUp:
      stabilizationWindowSeconds: 0
      policies:
      - type: Percent
        value: 100
        periodSeconds: 15
      - type: Pods
        value: 2
        periodSeconds: 15
```

### Deploy to Kubernetes

```bash
# Apply configurations
kubectl apply -f namespace.yaml
kubectl apply -f catalog-service.yaml
kubectl apply -f catalog-deployment.yaml
kubectl apply -f catalog-hpa.yaml

# Watch auto-scaling in action
kubectl get hpa -n gearify --watch

# See all pods
kubectl get pods -n gearify

# Generate load to trigger scaling
kubectl run -i --tty load-generator --rm --image=busybox --restart=Never -- /bin/sh
# Inside the pod:
while true; do wget -q -O- http://catalog-svc.gearify.svc.cluster.local/api/products; done
```

---

## Comparison Table

| Solution | Complexity | Auto-Discovery | Health Checks | Cost | Best For |
|----------|------------|----------------|---------------|------|----------|
| **Kubernetes** | Medium | ✅ Built-in | ✅ Yes | Medium | Production, multi-cloud |
| **AWS ECS + ALB** | Low | ✅ Built-in | ✅ Yes | High | AWS-only workloads |
| **Consul + YARP** | High | ✅ Yes | ✅ Yes | Low | Multi-platform, hybrid |
| **Docker Swarm** | Low | ✅ Built-in | ✅ Yes | Low | Small deployments |

---

## Summary

### The Key Insight

**Don't hardcode service addresses.** Use a **service discovery** mechanism that:

1. ✅ Tracks all running instances
2. ✅ Performs health checks
3. ✅ Automatically updates routing
4. ✅ Load balances traffic
5. ✅ Handles failures gracefully

### For Gearify, Use:

**Development:** Docker Compose (current setup - fine for dev)

**Production:** Kubernetes with HPA (Horizontal Pod Autoscaler)

**Why Kubernetes?**
- Industry standard
- Built-in service discovery
- Automatic load balancing
- Health checking
- Auto-scaling
- Multi-cloud support
- Rich ecosystem

**No code changes needed!** Just deploy to Kubernetes and it handles everything automatically. 🚀
