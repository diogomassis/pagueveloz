import http from 'k6/http';
import { check, sleep } from 'k6';

export const options = {
    vus: 20,
    duration: '20s',
    thresholds: {
        http_req_failed: ['rate<0.05'],
        http_req_duration: ['p(95)<1000'],
    },
};

const BASE = __ENV.BASE_URL || 'http://localhost:9999';

function rnd() {
    return `${Date.now()}-${Math.random().toString(36).substring(2, 8)}`;
}

export default function () {
    const health = http.get(`${BASE}/health`);
    check(health, {
        'health is 200': (r) => r.status === 200,
    });

    const accountId = `acc-${rnd()}`;
    const createAcc = http.post(
        `${BASE}/api/accounts`,
        JSON.stringify({ ClientId: 'k6', AccountId: accountId, InitialBalance: 1000, CreditLimit: 0 }),
        { headers: { 'Content-Type': 'application/json' } }
    );
    check(createAcc, { 'create account 2xx': (r) => r.status >= 200 && r.status < 300 });

    const txId = `tx-${rnd()}`;
    const createTx = http.post(
        `${BASE}/api/transactions`,
        JSON.stringify({ Operation: 'Debit', AccountId: accountId, Amount: 10, Currency: 'BRL', ReferenceId: txId }),
        { headers: { 'Content-Type': 'application/json', 'Idempotency-Key': txId } }
    );
    check(createTx, { 'transaction 2xx': (r) => r.status >= 200 && r.status < 300 });

    sleep(1);
}
