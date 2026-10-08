# DevOps Re-examination – ASP.NET Core CRUD on Docker & Kubernetes

**Student:** Aung Thu Min (100006979)
**Module:** IT Security Management (ITSM) & DevOps – Re-examination 2025/2026

A CRUD REST API written in C# (ASP.NET Core, .NET 10) with a MariaDB database,
containerised with Docker, orchestrated with Docker Compose and deployed to
Kubernetes (Minikube), load-tested with k6 and checked for self-healing.

## Repository structure

| Path | Contents |
|---|---|
| `app/CrudApp/` | ASP.NET Core minimal API (EF Core 9 + Pomelo MySQL provider) |
| `Dockerfile` | Multi-stage build: `dotnet/sdk:10.0` → `dotnet/aspnet:10.0-alpine` |
| `.dockerignore` | Excludes build output, Git data and non-app folders from the build context |
| `docker-compose.yaml` | App + MariaDB 11.4, named network, named volume, healthchecks |
| `k8s/` | Namespace, MariaDB (Secret, PVC, Deployment, Service), app Deployment, NodePort Service, HPA |
| `tests/` | k6 load-test script and the Pod manifest that runs it inside the cluster |
| `docs/` | k6 HTML report and CPU/memory metrics recorded during the load test |

## API endpoints

| Method | Endpoint | Description |
|---|---|---|
| GET | `/health` | Liveness – app process is up |
| GET | `/health/db` | Readiness – database is reachable |
| POST | `/items` | Create an item (`name`, `description`) |
| GET | `/items` | List all items |
| GET | `/items/{id}` | Get one item |
| PUT | `/items/{id}` | Update an item |
| DELETE | `/items/{id}` | Delete an item |

## Run with Docker Compose

```bash
docker compose up -d --build
docker compose ps            # both services should be (healthy)
curl http://localhost:8080/health
docker compose down
```

## Deploy to Minikube

```bash
minikube start --driver=docker
minikube addons enable metrics-server
docker build -t crudapp:1.0 .
minikube image load crudapp:1.0

kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/mariadb.yaml
kubectl apply -f k8s/deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/hpa.yaml

kubectl get pods,svc,hpa -n crud-app
minikube service crud-app -n crud-app --url
```

## Load test (k6, inside the cluster)

```bash
kubectl create configmap k6-script -n crud-app --from-file=tests/load-test.js
kubectl apply -f tests/k6-pod.yaml
kubectl logs -f k6-load-test -n crud-app
kubectl cp crud-app/k6-load-test:/results/k6-report.html docs/k6-report.html
```

**Result (500 VUs, 90 s):** 89,808 requests, ~988 req/s, 0.00 % errors,
p95 latency 703 ms, 100 % of checks passed. The HPA scaled the app from 1 to 4