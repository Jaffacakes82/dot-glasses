// Hand-written IndexedDB wrapper — deliberately no external JS dependency, so DotGlasses.App
// keeps its "Contracts project reference only" rule intact (this lives entirely inside the App
// project, not a new package/reference). Two object stores:
//   "outbox" — every queued offline write, business data and batched client logs alike.
//   "kv"     — small durable key/value blobs that must survive a page refresh: the auth token
//              and the cached reference-data/catalogue payload. Both are what make the login
//              screen's "log in once online, then work fully offline" promise actually true;
//              before this store existed the token was in-memory only and reference data was
//              re-fetched every session, so any refresh forced a re-login that needed a network.
window.dotGlassesIdb = (function () {
    const dbName = 'dotglasses-outbox';
    const storeName = 'outbox';
    const kvStoreName = 'kv';
    // Bumped 1 -> 2 to add the kv store. onupgradeneeded creates only what's missing, so an
    // existing device keeps its queued outbox items across the upgrade rather than losing them.
    const dbVersion = 2;
    let dbPromise = null;

    function openDb() {
        if (dbPromise) {
            return dbPromise;
        }

        dbPromise = new Promise((resolve, reject) => {
            const request = indexedDB.open(dbName, dbVersion);
            request.onupgradeneeded = (event) => {
                const db = event.target.result;
                if (!db.objectStoreNames.contains(storeName)) {
                    db.createObjectStore(storeName, { keyPath: 'id' });
                }
                if (!db.objectStoreNames.contains(kvStoreName)) {
                    db.createObjectStore(kvStoreName);
                }
            };
            request.onsuccess = () => resolve(request.result);
            request.onerror = () => reject(request.error);
        });

        return dbPromise;
    }

    return {
        enqueue: async function (itemJson) {
            const item = JSON.parse(itemJson);
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readwrite');
                tx.objectStore(storeName).put(item);
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        getPending: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readonly');
                const request = tx.objectStore(storeName).getAll();
                request.onsuccess = () => {
                    // 'Failed' is terminal (permanent 4xx — see SyncService.SyncItemAsync), not
                    // retryable, so it's excluded here alongside 'Synced' — otherwise SyncService
                    // re-POSTs the same permanently-invalid payload on every sync cycle forever.
                    const items = request.result.filter((i) => i.status !== 'Synced' && i.status !== 'Failed');
                    resolve(JSON.stringify(items));
                };
                request.onerror = () => reject(request.error);
            });
        },

        getFailed: async function () {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readonly');
                const request = tx.objectStore(storeName).getAll();
                request.onsuccess = () => {
                    const items = request.result.filter((i) => i.status === 'Failed');
                    resolve(JSON.stringify(items));
                };
                request.onerror = () => reject(request.error);
            });
        },

        updateStatus: async function (id, status, error) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readwrite');
                const store = tx.objectStore(storeName);
                const getReq = store.get(id);
                getReq.onsuccess = () => {
                    const item = getReq.result;
                    if (!item) {
                        return;
                    }
                    item.status = status;
                    item.lastError = error || null;
                    item.attemptCount = (item.attemptCount || 0) + 1;
                    store.put(item);
                };
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        getById: async function (id) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readonly');
                const request = tx.objectStore(storeName).get(id);
                request.onsuccess = () => resolve(request.result ? JSON.stringify(request.result) : null);
                request.onerror = () => reject(request.error);
            });
        },

        // Removes an outbox item outright. Only used to discard a permanently-failed record the
        // technician has chosen to abandon — a synced or pending item is never deleted this way.
        remove: async function (id) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readwrite');
                tx.objectStore(storeName).delete(id);
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        // Re-queues a permanently-failed item after the technician has corrected it: overwrites
        // the payload and puts the status back to PendingSync so SyncService picks it up again.
        // Keeps the same id, so the server-side idempotent upsert still applies.
        requeue: async function (id, payloadJson) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(storeName, 'readwrite');
                const store = tx.objectStore(storeName);
                const getReq = store.get(id);
                getReq.onsuccess = () => {
                    const item = getReq.result;
                    if (!item) {
                        return;
                    }
                    if (payloadJson) {
                        item.payloadJson = payloadJson;
                    }
                    item.status = 'PendingSync';
                    item.lastError = null;
                    store.put(item);
                };
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        kvGet: async function (key) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(kvStoreName, 'readonly');
                const request = tx.objectStore(kvStoreName).get(key);
                request.onsuccess = () => resolve(request.result ?? null);
                request.onerror = () => reject(request.error);
            });
        },

        kvSet: async function (key, value) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(kvStoreName, 'readwrite');
                tx.objectStore(kvStoreName).put(value, key);
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        kvRemove: async function (key) {
            const db = await openDb();
            return new Promise((resolve, reject) => {
                const tx = db.transaction(kvStoreName, 'readwrite');
                tx.objectStore(kvStoreName).delete(key);
                tx.oncomplete = () => resolve();
                tx.onerror = () => reject(tx.error);
            });
        },

        registerConnectivityCallback: function (dotnetRef) {
            window.addEventListener('online', () => dotnetRef.invokeMethodAsync('OnOnline'));
        },

        isOnline: function () {
            return navigator.onLine;
        },
    };
})();
