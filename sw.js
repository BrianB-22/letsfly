const CACHE = 'simletsfly-v83';
// Large, rarely-changing data files live in a separate cache that survives
// app-shell version bumps (airports.json is ~19 MB — don't re-download per deploy)
const DATA_CACHE = 'simletsfly-data-v1';
const DATA_FILES = ['/airports.json'];
const STATIC = [
  '/',
  '/index.html',
  '/flights.html',
  '/report.html',
  '/help.html',
  '/banner.json',
  '/whatsnew.html',
  '/favicon.svg',
  '/icon-192.svg',
  '/icon-512.svg',
  '/manifest.json',
];

self.addEventListener('install', e => {
  e.waitUntil(
    Promise.all([
      caches.open(CACHE).then(c => c.addAll(STATIC)),
      // Only fetch data files if not already cached
      caches.open(DATA_CACHE).then(async c => {
        for (const url of DATA_FILES) {
          if (!(await c.match(url))) await c.add(url);
        }
      }),
    ]).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE && k !== DATA_CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // Always go to network for: Supabase, weather APIs, fonts, analytics, map tiles
  const passThrough = [
    'supabase.co',
    'aviationweather.gov',
    'rainviewer.com',
    'tilecache.rainviewer.com',
    'googleapis.com',
    'gstatic.com',
    'googletagmanager.com',
    'paypal.com',
    'flightplandatabase.com',
    'open-meteo.com',
    'jsdelivr.net',
    'unpkg.com',
    'cartocdn.com',       // Leaflet map tiles — browser HTTP cache handles these
    'openstreetmap.org',
  ];
  if (passThrough.some(h => url.hostname.includes(h))) return;

  // Data files: cache-first from the persistent data cache
  if (DATA_FILES.includes(url.pathname)) {
    e.respondWith(
      caches.open(DATA_CACHE).then(c =>
        c.match(e.request).then(cached => cached || fetch(e.request).then(res => {
          if (res.ok) c.put(e.request, res.clone());
          return res;
        }))
      )
    );
    return;
  }

  // Network-first for HTML navigations so page updates are instant
  if (e.request.mode === 'navigate' || e.request.destination === 'document') {
    e.respondWith(
      fetch(e.request).then(res => {
        if (res.ok) {
          const clone = res.clone();
          caches.open(CACHE).then(c => c.put(e.request, clone));
        }
        return res;
      }).catch(() => caches.match(e.request, { ignoreSearch: true }))
    );
    return;
  }

  // Cache-first for all other static assets (JS, CSS, images, JSON)
  e.respondWith(
    caches.match(e.request).then(cached => {
      if (cached) return cached;
      return fetch(e.request).then(res => {
        if (res.ok && e.request.method === 'GET') {
          const clone = res.clone();
          caches.open(CACHE).then(c => c.put(e.request, clone));
        }
        return res;
      });
    })
  );
});
