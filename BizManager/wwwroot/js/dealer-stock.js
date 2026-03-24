// Bayi Stoku — Koli/Paket/Birim fiyatları ile tam CRUD

async function renderDealerStock() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Bayi Stoku &amp; Fiyatları</h2>
      <button class="btn btn-primary btn-sm" onclick="openStockModal()">+ Stok Girişi Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Bayi</th><th>Ürün</th><th>Marka</th><th>Stok</th><th>Koli Fiyatı</th><th>Paket Fiyatı</th><th>Birim Fiyatı</th><th>Son Güncelleme</th><th>İşlemler</th></tr></thead>
        <tbody id="stock-list"><tr><td colspan="9" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadStock();
}

async function loadStock() {
  try {
    const items = await api('/dealer-stock');
    const tbody = document.getElementById('stock-list');
    if (!items.length) {
      tbody.innerHTML = '<tr><td colspan="9"><div class="empty-state"><p>Henüz stok kaydı yok. Bir bayiye ürün ekleyin.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = items.map(s => `
      <tr>
        <td><strong>${s.dealer?.name || '—'}</strong></td>
        <td>${s.product?.name || '—'}</td>
        <td>${s.product?.brand?.name ? `<span class="badge badge-info">${s.product.brand.name}</span>` : '—'}</td>
        <td>
          <span class="badge ${s.stockQuantity === 0 ? 'badge-missing' : s.stockQuantity < 5 ? 'badge-preparing' : 'badge-delivered'}">
            ${s.stockQuantity} adet
          </span>
        </td>
        <td><strong>₺${fmtMoney(s.casePrice)}</strong></td>
        <td><strong>₺${fmtMoney(s.packPrice)}</strong></td>
        <td><strong>₺${fmtMoney(s.unitPrice)}</strong></td>
        <td>${fmtDate(s.lastUpdated)}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openStockModal(${JSON.stringify(s).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteStock(${s.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Stok yüklenemedi: ' + e.message, 'danger');
  }
}

async function openStockModal(stock = null) {
  try {
    const [dealers, products] = await Promise.all([api('/dealers'), api('/products')]);
    openModal(stock ? 'Stok Girişini Düzenle' : 'Stok Girişi Ekle', `
      <form id="stock-form">
        <div class="form-group">
          <label>Bayi *</label>
          <select class="form-control" name="dealerId" required>
            <option value="">— Bayi Seçin —</option>
            ${dealers.map(d => `<option value="${d.id}" ${stock?.dealerId == d.id ? 'selected' : ''}>${d.name}</option>`).join('')}
          </select>
        </div>
        <div class="form-group">
          <label>Ürün *</label>
          <select class="form-control" name="productId" required>
            <option value="">— Ürün Seçin —</option>
            ${products.map(p => `<option value="${p.id}" ${stock?.productId == p.id ? 'selected' : ''}>${p.name}${p.code ? ' (' + p.code + ')' : ''}</option>`).join('')}
          </select>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Stok Miktarı *</label>
            <input class="form-control" name="stockQuantity" type="number" min="0" value="${stock?.stockQuantity ?? 0}" required placeholder="0">
          </div>
          <div class="form-group">
            <label>Koli Fiyatı (₺)</label>
            <input class="form-control" name="casePrice" type="number" step="0.01" min="0" value="${stock?.casePrice ?? ''}" placeholder="0,00">
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Paket Fiyatı (₺)</label>
            <input class="form-control" name="packPrice" type="number" step="0.01" min="0" value="${stock?.packPrice ?? ''}" placeholder="0,00">
          </div>
          <div class="form-group">
            <label>Birim Fiyatı (₺)</label>
            <input class="form-control" name="unitPrice" type="number" step="0.01" min="0" value="${stock?.unitPrice ?? ''}" placeholder="0,00">
          </div>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${stock ? 'Değişiklikleri Kaydet' : 'Stoğa Ekle'}</button>
        </div>
      </form>`);

    document.getElementById('stock-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = {
        dealerId:      +fd.get('dealerId'),
        productId:     +fd.get('productId'),
        stockQuantity: +fd.get('stockQuantity'),
        casePrice:     +fd.get('casePrice') || 0,
        packPrice:     +fd.get('packPrice') || 0,
        unitPrice:     +fd.get('unitPrice') || 0
      };
      try {
        if (stock) {
          await api(`/dealer-stock/${stock.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Stok girişi güncellendi', 'success');
        } else {
          await api('/dealer-stock', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Stok girişi oluşturuldu', 'success');
        }
        closeModal();
        await loadStock();
      } catch (err) {
        toast('Stok girişi kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Form açılamadı: ' + e.message, 'danger');
  }
}

async function deleteStock(id) {
  if (!confirm('Bu stok girişini silmek istiyor musunuz?')) return;
  try {
    await api(`/dealer-stock/${id}`, { method: 'DELETE' });
    toast('Stok girişi silindi');
    await loadStock();
  } catch (e) {
    toast('Stok girişi silinemedi: ' + e.message, 'danger');
  }
}
