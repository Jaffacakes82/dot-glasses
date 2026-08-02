// Hand-written IndexedDB wrapper for the offline outbox — deliberately no external JS
// dependency, so DotGlasses.App keeps its "Contracts project reference only" rule intact
// (this lives entirely inside the App project, not a new package/reference). One object store
// ("outbox") holds every queued offline write, business data and batched client logs alike.
window.dotGlassesIdb = (function () {
    const dbName = 'dotglasses-outbox';
    const storeName = 'outbox';
    const dbVersion = 1;
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
                    const items = request.result.filter((i) => i.status !== 'Synced');
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

        registerConnectivityCallback: function (dotnetRef) {
            window.addEventListener('online', () => dotnetRef.invokeMethodAsync('OnOnline'));
        },

        isOnline: function () {
            return navigator.onLine;
        },
    };
})();
