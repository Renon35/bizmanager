// ── Global State & Helpers ──────────────────────────────────────────────────
const API = '/api';

async function api(path, opts = {}) {
  const res = await fetch(API + path, opts);
  if (!res.ok) {
    const err = await res.text();
    throw new Error(err || res.statusText);
  }
  if (res.status === 204) return null;
  const ct = res.headers.get('content-type') || '';
  if (ct.includes('application/pdf')) return res.blob();
  return res.json();
}

function toast(msg, type = '') {
  const el = document.createElement('div');
  el.className = `toast ${type}`;
  el.textContent = msg;
  document.getElementById('toast-container').appendChild(el);
  setTimeout(() => el.remove(), 3000);
}

function openModal(title, html, onSubmit) {
  document.getElementById('modal-title').textContent = title;
  document.getElementById('modal-body').innerHTML = html;
  document.getElementById('modal-overlay').classList.remove('hidden');
  if (onSubmit) {
    const form = document.getElementById('modal-body').querySelector('form');
    if (form) form.addEventListener('submit', onSubmit);
  }
}

function closeModal() {
  document.getElementById('modal-overlay').classList.add('hidden');
}

function fmt(val) {
  if (val === null || val === undefined) return '—';
  return val;
}

function fmtDate(d) {
  if (!d) return '—';
  return new Date(d).toLocaleDateString('tr-TR');
}

function fmtMoney(n) {
  return Number(n || 0).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
}

// ── Router ──────────────────────────────────────────────────────────────────
const pages = {
  dashboard: { title: 'Panel', fn: () => renderDashboard() },
  brands: { title: 'Markalar', fn: () => renderBrands() },
  catalogs: { title: 'Kataloglar', fn: () => renderCatalogs() },
  dealers: { title: 'Bayiler', fn: () => renderDealers() },
  products: { title: 'Ürünler', fn: () => renderProducts() },
  'dealer-stock': { title: 'Bayi Stoku', fn: () => renderDealerStock() },
  'purchase-orders': { title: 'Satın Alma Siparişleri', fn: () => renderPurchaseOrders() },
  shipments: { title: 'Sevkiyatlar', fn: () => renderShipments() },
  'sales-reps': { title: 'Satış Temsilcileri', fn: () => renderSalesReps() },
  customers: { title: 'Müşteriler', fn: () => renderCustomers() },
  quotations: { title: 'Satış Teklifleri', fn: () => renderQuotations() },
  'sales-orders': { title: 'Satış Siparişleri', fn: () => renderSalesOrders() },
  'sales-shipments': { title: 'Satış Sevkiyatları', fn: () => renderSalesShipments() },
  sales: { title: 'Satış Gelirleri', fn: () => renderSales() },
  invoices: { title: 'Faturalar', fn: () => renderInvoices() },
  import:   { title: 'Excel İçe Aktar', fn: () => renderImport() },
  'catalog-analysis': { title: 'Katalog Analizi', fn: () => renderCatalogAnalysis() }
};

function renderCatalogAnalysis() {
  if (typeof CatalogAnalysisController !== 'undefined') {
    CatalogAnalysisController.render();
  }
}

function navigate(hash) {
  const page = hash.replace('#', '') || 'dashboard';
  const p = pages[page];
  if (!p) return;
  document.getElementById('page-title').textContent = p.title;
  document.querySelectorAll('.nav-item').forEach(el => {
    el.classList.toggle('active', el.dataset.page === page);
  });
  closeModal();
  p.fn();
}

window.addEventListener('hashchange', () => navigate(location.hash));
window.addEventListener('load', () => navigate(location.hash || '#dashboard'));

// Saat
function updateClock() {
  document.getElementById('current-time').textContent =
    new Date().toLocaleString('tr-TR', { dateStyle: 'medium', timeStyle: 'short' });
}
setInterval(updateClock, 1000);
updateClock();
