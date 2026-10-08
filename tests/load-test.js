import http from 'k6/http';
import { check, sleep } from 'k6';

// Inside the cluster the app is reachable via its Service name
const BASE_URL = __ENV.BASE_URL || 'http://crud-app:8080';
const JSON_HEADERS = { 'Content-Type': 'application/json' };

export const options = {
  scenarios: {
    crud_load: {
      executor: 'ramping-vus',
      startVUs: 0,
      stages: [
        { duration: '20s', target: 500 }, // ramp up to 500 virtual users
        { duration: '60s', target: 500 }, // hold 500 VUs for 60 seconds
        { duration: '10s', target: 0 },   // ramp down
      ],
      gracefulRampDown: '10s',
    },
  },
  thresholds: {
    http_req_failed: ['rate<0.01'],     // less than 1% errors
    http_req_duration: ['p(95)<2000'],  // 95% of requests under 2 s
  },
};

export default function () {
  // Health check
  const health = http.get(`${BASE_URL}/health`, { tags: { name: 'GET /health' } });
  check(health, { 'health is 200': (r) => r.status === 200 });

  // Create
  const created = http.post(
    `${BASE_URL}/items`,
    JSON.stringify({ name: `item-${__VU}-${__ITER}`, description: 'load test' }),
    { headers: JSON_HEADERS, tags: { name: 'POST /items' } }
  );
  check(created, { 'create is 201': (r) => r.status === 201 });
  if (created.status !== 201) {
    sleep(1);
    return;
  }
  const id = created.json('id');

  // Read all
  const list = http.get(`${BASE_URL}/items`, { tags: { name: 'GET /items' } });
  check(list, { 'read all is 200': (r) => r.status === 200 });

  // Read one
  const one = http.get(`${BASE_URL}/items/${id}`, { tags: { name: 'GET /items/{id}' } });
  check(one, { 'read one is 200': (r) => r.status === 200 });

  // Update
  const updated = http.put(
    `${BASE_URL}/items/${id}`,
    JSON.stringify({ name: `updated-${__VU}`, description: 'updated by load test' }),
    { headers: JSON_HEADERS, tags: { name: 'PUT /items/{id}' } }
  );
  check(updated, { 'update is 200': (r) => r.status === 200 });

  // Delete
  const deleted = http.del(`${BASE_URL}/items/${id}`, null, { tags: { name: 'DELETE /items/{id}' } });
  check(deleted, { 'delete is 200': (r) => r.status === 200 });

  sleep(1);
}