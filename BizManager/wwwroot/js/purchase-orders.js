// Satın Alma Siparişleri — Dinamik ürün satırlarıyla tam CRUD

let poItems = [];
let _poProducts = [];

async function renderPurchaseOrders() {
  const content = document.getElementById('page-content');
  content.innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Satın Alma Siparişleri</h2>
      <button class="btn btn-primary btn-sm" onclick="openPoModal()">+ Yeni Sipariş</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr>
          <th>Sipariş #</th><th>Bayi</th><th>Tarih</th>
          <th>Durum</th><th>Kalemler</th><th>Toplam</th><th>İşlemler</th>
        </tr></thead>
        <tbody id="po-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadPurchaseOrders();
}

async function loadPurchaseOrders() {
  try {
    const orders = await api('/purchase-orders');
    _poProducts = await api('/products');
    const tbody = document.getElementById('po-list');
    if (!orders.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Henüz sipariş yok. Oluşturmak için "+ Yeni Sipariş" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = orders.map(o => {
      const total = (o.items || []).reduce((s, i) => s + (i.totalPrice || i.quantity * i.unitPrice), 0);
      return `<tr>
        <td><strong>${o.orderNumber}</strong></td>
        <td>${o.dealer?.name || '—'}</td>
        <td>${fmtDate(o.orderDate)}</td>
        <td><span class="badge badge-${o.status}">${statusTR(o.status)}</span></td>
        <td>${(o.items || []).length} kalem</td>
        <td><strong>₺${fmtMoney(total)}</strong></td>
        <td><div class="section-actions">
          <button class="btn btn-ghost btn-sm" onclick='editPo(${o.id})'>Düzenle</button>
          <button class="btn btn-danger btn-sm" onclick="deletePo(${o.id})">Sil</button>
        </div></td>
      </tr>`;
    }).join('');
  } catch (e) {
    toast('Siparişler yüklenemedi: ' + e.message, 'danger');
  }
}

async function editPo(id) {
  const order = await api(`/purchase-orders/${id}`);
  openPoModal(order);
}

async function openPoModal(order = null) {
  try {
    const [dealers, products] = await Promise.all([api('/dealers'), api('/products')]);
    _poProducts = products;

    poItems = (order?.items || []).map(i => ({
      productId: String(i.productId),
      productName: products.find(p => p.id === i.productId)?.name || `Ürün #${i.productId}`,
      quantity: i.quantity,
      unitPrice: i.unitPrice
    }));

    const dealerOptions = dealers.map(d =>
      `<option value="${d.id}" ${order?.dealerId == d.id ? 'selected' : ''}>${d.name}</option>`
    ).join('');
    const productOptions = products.map(p =>
      `<option value="${p.id}">${p.name}${p.code ? ' (' + p.code + ')' : ''}</option>`
    ).join('');

    const statusLabels = { preparing: 'Hazırlanıyor', shipped: 'Sevk Edildi', delivered: 'Teslim Edildi' };

    openModal(order ? 'Satın Alma Siparişini Düzenle' : 'Yeni Satın Alma Siparişi', `
      <form id="po-form">
        <div class="form-row">
          <div class="form-group">
            <label>Sipariş Numarası *</label>
            <input class="form-control" name="orderNumber" value="${order?.orderNumber || ''}" placeholder="ör. SPS-2026-001" required>
          </div>
          <div class="form-group">
            <label>Sipariş Durumu</label>
            <select class="form-control" name="status">
              ${['preparing','shipped','delivered'].map(s =>
                `<option value="${s}" ${(order?.status || 'preparing') === s ? 'selected' : ''}>${statusLabels[s]}</option>`
              ).join('')}
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Bayi *</label>
            <select class="form-control" name="dealerId" required>${dealerOptions}</select>
          </div>
          <div class="form-group">
            <label>Sipariş Tarihi</label>
            <input class="form-control" name="orderDate" type="date"
              value="${order ? new Date(order.orderDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]}">
          </div>
        </div>
        <div style="margin:12px 0 6px"><strong>Sipariş Kalemleri</strong></div>
        <div id="po-items-list" class="items-list"></div>
        <div style="display:flex;gap:8px;align-items:center;margin-top:10px">
          <select class="form-control" id="po-add-product" style="flex:1">${productOptions}</select>
          <button type="button" class="btn btn-ghost btn-sm" onclick="addPoItem()">+ Kalem Ekle</button>
        </div>
        <div id="po-total" style="text-align:right;font-weight:600;margin-top:10px;font-size:15px;color:#1a56db"></div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${order ? 'Değişiklikleri Kaydet' : 'Sipariş Oluştur'}</button>
        </div>
      </form>`);

    renderPoItems();

    document.getElementById('po-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = {
        orderNumber: fd.get('orderNumber'),
        dealerId: +fd.get('dealerId'),
        orderDate: new Date(fd.get('orderDate')).toISOString(),
        status: fd.get('status'),
        items: poItems.map(i => ({
          productId: +i.productId,
          quantity: +i.quantity,
          unitPrice: +i.unitPrice,
          totalPrice: +(+i.quantity * +i.unitPrice).toFixed(2)
        }))
      };
      try {
        if (order) {
          await api(`/purchase-orders/${order.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Sipariş güncellendi', 'success');
        } else {
          await api('/purchase-orders', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Sipariş oluşturuldu', 'success');
        }
        closeModal();
        await loadPurchaseOrders();
      } catch (err) {
        toast('Sipariş kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (err) {
    toast('Sipariş formu açılamadı: ' + err.message, 'danger');
  }
}

function addPoItem() {
  const sel = document.getElementById('po-add-product');
  const productId = sel.value;
  const productName = sel.options[sel.selectedIndex].text;
  poItems.push({ productId, productName, quantity: 1, unitPrice: 0 });
  renderPoItems();
}

function removePoItem(i) {
  poItems.splice(i, 1);
  renderPoItems();
}

function updatePoTotal() {
  const total = poItems.reduce((s, i) => s + (+i.quantity * +i.unitPrice), 0);
  const el = document.getElementById('po-total');
  if (el) el.textContent = `Sipariş Toplamı: ₺${fmtMoney(total)}`;
}

function renderPoItems() {
  const list = document.getElementById('po-items-list');
  if (!list) return;
  if (!poItems.length) {
    list.innerHTML = '<div style="color:#94a3b8;font-size:13px;padding:8px 0">Henüz kalem eklenmedi.</div>';
    updatePoTotal();
    return;
  }
  list.innerHTML = poItems.map((item, i) => `
    <div class="item-row" style="grid-template-columns:2fr 80px 110px auto;gap:8px;display:grid;align-items:center;margin-bottom:6px">
      <span style="font-size:13px;font-weight:500">${item.productName}</span>
      <input class="form-control" type="number" min="1" value="${item.quantity}"
        oninput="poItems[${i}].quantity=this.value; updatePoTotal()">
      <input class="form-control" type="number" step="0.01" min="0" placeholder="Birim fiyat" value="${item.unitPrice}"
        oninput="poItems[${i}].unitPrice=this.value; updatePoTotal()">
      <button type="button" class="btn btn-danger btn-sm" onclick="removePoItem(${i})">✕</button>
    </div>`).join('');
  updatePoTotal();
}

async function deletePo(id) {
  if (!confirm('Bu satın alma siparişini silmek istiyor musunuz? Kalemleri ve sevkiyatı da silinecektir.')) return;
  try {
    await api(`/purchase-orders/${id}`, { method: 'DELETE' });
    toast('Sipariş silindi');
    await loadPurchaseOrders();
  } catch (e) {
    toast('Sipariş silinemedi: ' + e.message, 'danger');
  }
}
