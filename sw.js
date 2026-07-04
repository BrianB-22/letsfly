const CACHE = 'simletsfly-v80';
const STATIC = [
  '/',
  '/index.html',
  '/flights.html',
  '/report.html',
  '/help.html',
  '/airports.json',
  '/banner.json',
  '/whatsnew.html',
  '/favicon.svg',
  '/icon-192.svg',
  '/icon-512.svg',
  '/manifest.json',
];

self.addEventListener('install', e => {
  e.waitUntil(
    caches.open(CACHE).then(c => c.addAll(STATIC)).then(() => self.skipWaiting())
  );
});

self.addEventListener('activate', e => {
  e.waitUntil(
    caches.keys().then(keys =>
      Promise.all(keys.filter(k => k !== CACHE).map(k => caches.delete(k)))
    ).then(() => self.clients.claim())
  );
});

self.addEventListener('fetch', e => {
  const url = new URL(e.request.url);

  // Always go to network for: Supabase, weather APIs, fonts, analytics
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
  ];
  if (passThrough.some(h => url.hostname.includes(h))) return;

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
