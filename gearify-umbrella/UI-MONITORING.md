# Gearify UI Monitoring Guide

Monitor all your Gearify services through beautiful web-based UIs!

## 🎯 Container Management

### **Portainer** - Docker Container Management
- **URL**: http://localhost:9000
- **Features**:
  - View all containers status and health
  - Start/Stop/Restart containers
  - View real-time logs
  - Monitor CPU/Memory usage
  - Execute commands in containers
  - Manage volumes and networks
  - Container stats and metrics

**First Time Setup**:
1. Go to http://localhost:9000
2. Create an admin account (username + password)
3. Select "Get Started" to connect to local Docker
4. You'll see all 22 Gearify containers!

---

## 📊 Observability UIs

### **Grafana** - Metrics Dashboards
- **URL**: http://localhost:3000
- **Login**: admin / admin
- **Features**:
  - Real-time metrics visualization
  - Custom dashboards
  - Alerts and notifications
  - Service performance monitoring

### **Prometheus** - Metrics Database
- **URL**: http://localhost:9090
- **Features**:
  - Query metrics with PromQL
  - View targets health
  - Service discovery status
  - Metrics exploration

### **Jaeger** - Distributed Tracing
- **URL**: http://localhost:16686
- **Features**:
  - View service traces
  - Find bottlenecks
  - Analyze request flow
  - Performance analysis

### **Seq** - Structured Logging
- **URL**: http://localhost:5341
- **Login**: admin / admin
- **Features**:
  - Search logs with queries
  - Filter by service
  - View log timelines
  - Error tracking

---

## 📧 Development Tools

### **MailHog** - Email Testing
- **URL**: http://localhost:8025
- **Features**:
  - View all sent emails
  - Test email templates
  - No real emails sent
  - Perfect for development

---

## 🌐 Application UIs

### **Gearify Web App**
- **URL**: http://localhost:4200
- **Description**: Main customer-facing application

### **API Gateway**
- **URL**: http://localhost:8080
- **Swagger**: http://localhost:8080/swagger (if enabled)

---

## 🔍 Quick Health Check Dashboard

Create a simple HTML dashboard to see all UIs in one place:

```html
<!DOCTYPE html>
<html>
<head>
    <title>Gearify Monitoring Dashboard</title>
    <style>
        body { font-family: Arial, sans-serif; padding: 20px; background: #f5f5f5; }
        .grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(300px, 1fr)); gap: 20px; }
        .card { background: white; padding: 20px; border-radius: 8px; box-shadow: 0 2px 4px rgba(0,0,0,0.1); }
        .card h3 { margin-top: 0; color: #333; }
        .card a { display: block; margin: 10px 0; color: #0066cc; text-decoration: none; }
        .card a:hover { text-decoration: underline; }
        .category { color: #666; font-size: 12px; text-transform: uppercase; }
    </style>
</head>
<body>
    <h1>🎯 Gearify Monitoring Dashboard</h1>

    <div class="grid">
        <div class="card">
            <div class="category">Container Management</div>
            <h3>🐳 Portainer</h3>
            <a href="http://localhost:9000" target="_blank">Open Portainer →</a>
            <p>Manage all Docker containers, view logs, stats, and more</p>
        </div>

        <div class="card">
            <div class="category">Metrics & Dashboards</div>
            <h3>📊 Grafana</h3>
            <a href="http://localhost:3000" target="_blank">Open Grafana →</a>
            <p>Visualize metrics with beautiful dashboards</p>
        </div>

        <div class="card">
            <div class="category">Distributed Tracing</div>
            <h3>🔍 Jaeger</h3>
            <a href="http://localhost:16686" target="_blank">Open Jaeger →</a>
            <p>Trace requests across microservices</p>
        </div>

        <div class="card">
            <div class="category">Structured Logs</div>
            <h3>📝 Seq</h3>
            <a href="http://localhost:5341" target="_blank">Open Seq →</a>
            <p>Search and analyze application logs</p>
        </div>

        <div class="card">
            <div class="category">Metrics Query</div>
            <h3>📈 Prometheus</h3>
            <a href="http://localhost:9090" target="_blank">Open Prometheus →</a>
            <p>Query metrics with PromQL</p>
        </div>

        <div class="card">
            <div class="category">Email Testing</div>
            <h3>📧 MailHog</h3>
            <a href="http://localhost:8025" target="_blank">Open MailHog →</a>
            <p>View test emails sent by the application</p>
        </div>

        <div class="card">
            <div class="category">Application</div>
            <h3>🛍️ Web App</h3>
            <a href="http://localhost:4200" target="_blank">Open Web App →</a>
            <p>Main Gearify e-commerce application</p>
        </div>

        <div class="card">
            <div class="category">API</div>
            <h3>🚪 API Gateway</h3>
            <a href="http://localhost:8080" target="_blank">Open API Gateway →</a>
            <p>Central API entry point</p>
        </div>
    </div>

    <h2>📋 Service Health</h2>
    <div class="card">
        <pre id="status">Loading service status...</pre>
    </div>

    <script>
        // Auto-refresh service status
        async function checkServices() {
            const services = [
                { name: 'API Gateway', url: 'http://localhost:8080' },
                { name: 'Web App', url: 'http://localhost:4200' },
                { name: 'Grafana', url: 'http://localhost:3000' },
                { name: 'Jaeger', url: 'http://localhost:16686' },
                { name: 'Seq', url: 'http://localhost:5341' },
                { name: 'Prometheus', url: 'http://localhost:9090' },
                { name: 'MailHog', url: 'http://localhost:8025' },
                { name: 'Portainer', url: 'http://localhost:9000' }
            ];

            let status = 'Service Status (Last checked: ' + new Date().toLocaleTimeString() + ')\\n\\n';

            for (const service of services) {
                try {
                    const response = await fetch(service.url, { mode: 'no-cors' });
                    status += `✅ ${service.name.padEnd(20)} - Running\\n`;
                } catch (e) {
                    status += `❌ ${service.name.padEnd(20)} - Not accessible\\n`;
                }
            }

            document.getElementById('status').textContent = status;
        }

        checkServices();
        setInterval(checkServices, 30000); // Refresh every 30 seconds
    </script>
</body>
</html>
```

Save this as `dashboard.html` in your project root and open it in your browser!

---

## 📱 Mobile Access

All UIs are accessible from mobile devices on your local network:
- Replace `localhost` with your computer's IP address
- Example: `http://192.168.1.100:9000`

---

## 🔐 Default Credentials

| Service | URL | Username | Password |
|---------|-----|----------|----------|
| Portainer | http://localhost:9000 | (create on first visit) | (create on first visit) |
| Grafana | http://localhost:3000 | admin | admin |
| Seq | http://localhost:5341 | admin | admin |

---

## 🎨 UI Features Comparison

| Feature | Portainer | Grafana | Jaeger | Seq | Prometheus |
|---------|-----------|---------|--------|-----|------------|
| Container Management | ✅ | ❌ | ❌ | ❌ | ❌ |
| Logs Viewing | ✅ | ❌ | ❌ | ✅ | ❌ |
| Metrics | ✅ | ✅ | ❌ | ❌ | ✅ |
| Tracing | ❌ | ❌ | ✅ | ❌ | ❌ |
| Alerts | ❌ | ✅ | ❌ | ✅ | ✅ |
| Dashboards | ❌ | ✅ | ✅ | ✅ | ✅ |

---

## 🚀 Quick Start

1. **Start Portainer** (already running):
   ```bash
   docker compose up -d portainer
   ```

2. **Open Portainer**:
   - Go to http://localhost:9000
   - Create admin account
   - Select "Get Started"

3. **Explore Your Services**:
   - Click "Containers" to see all 22 services
   - Click any container to view logs, stats, or manage it

---

## 💡 Pro Tips

1. **Bookmark All UIs** in a folder for quick access
2. **Use Portainer** for quick restarts and log viewing
3. **Use Grafana** for performance monitoring over time
4. **Use Jaeger** when debugging slow requests
5. **Use Seq** for searching application errors
6. **Use MailHog** to verify email functionality

---

## 🔧 Troubleshooting

### Can't access Portainer?
```bash
docker logs gearify-portainer
docker restart gearify-portainer
```

### Forgot Grafana password?
```bash
docker exec -it gearify-grafana grafana-cli admin reset-admin-password admin
```

### Need to reset Portainer?
```bash
docker compose down portainer
docker volume rm gearify-umbrella_portainer-data
docker compose up -d portainer
```
