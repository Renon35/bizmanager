// Panel — Özet istatistikler ve son siparişler

async function renderDashboard() {
  const content = document.getElementById('page-content');
  content.innerHTML = `<div class="stats-grid">
    <div class="stat-card primary"><div class="label">Toplam Satış</div><div class="value" id="dash-total">—</div></div>
    <div class="stat-card warning"><div class="label">Satın Alma (Hazırlanıyor)</div><div class="value" id="dash-prep">—</div></div>
    <div class="stat-card info" style="--info:#3b82f6"><div class="label">Satın Alma (Sevkiyatta)</div><div class="value" id="dash-ship" style="color:#3b82f6">—</div></div>
    <div class="stat-card success"><div class="label">Satın Alma (Teslim Edildi)</div><div class="value" id="dash-del">—</div></div>
    <div class="stat-card danger"><div class="label">Eksik Faturalar</div><div class="value" id="dash-inv">—</div></div>
    <div class="stat-card warning"><div class="label">Düşük Stoklu Ürünler</div><div class="value" id="dash-low">—</div></div>
  </div>
  <div class="dashboard-grid">
    <div class="card">
      <div class="card-header"><h2>Son Satın Alma Siparişleri</h2></div>
      <div class="card-body"><table>
        <thead><tr><th>Sipariş #</th><th>Bayi</th><th>Tarih</th><th>Durum</th></tr></thead>
        <tbody id="dash-orders"></tbody>
      </table></div>
    </div>
    
    <div style="display: flex; flex-direction: column; gap: 24px;">
      <div class="card">
        <div class="card-header"><h2>⚠️ Uyarılar</h2></div>
        <div class="card-body" style="padding:16px"><div id="dash-alerts" class="alert-list"></div></div>
      </div>
      <div class="card" style="border: 1px solid #eab308; box-shadow: 0 4px 6px -1px rgba(234, 179, 8, 0.1);">
        <div class="card-header" style="background: rgba(234, 179, 8, 0.1); border-bottom: 1px solid #fef08a;">
          <h2 style="color: #ca8a04;">Bekleyen Teslimatlar (Satış Eksiklikleri)</h2>
        </div>
        <div class="card-body">
          <table>
            <thead><tr><th>Sipariş</th><th>Ürün</th><th>Eksik</th><th>Beklenen Tarih</th></tr></thead>
            <tbody id="dash-pending-deliveries"><tr><td colspan="4" style="text-align:center;color:#94a3b8">Yükleniyor...</td></tr></tbody>
          </table>
        </div>
      </div>
      <div class="card" style="border: 1px solid #f97316; box-shadow: 0 4px 6px -1px rgba(249, 115, 22, 0.1);">
        <div class="card-header" style="background: rgba(249, 115, 22, 0.1); border-bottom: 1px solid #ffedd5;">
          <h2 style="color: #c2410c;">Görseli Eksik Ürünler</h2>
        </div>
        <div class="card-body">
          <table>
            <thead><tr><th>Kod</th><th>Ürün Adı</th><th>Marka / Katalog</th><th>Koleksiyon</th></tr></thead>
            <tbody id="dash-missing-images"><tr><td colspan="4" style="text-align:center;color:#94a3b8">Yükleniyor...</td></tr></tbody>
          </table>
        </div>
      </div>
    </div>
  </div>`;

  try {
    const d = await api('/dashboard');
    document.getElementById('dash-total').textContent = '₺' + fmtMoney(d.totalSales);
    document.getElementById('dash-prep').textContent = d.pendingOrders;
    document.getElementById('dash-ship').textContent = d.shippedOrders;
    document.getElementById('dash-del').textContent = d.deliveredOrders;
    document.getElementById('dash-inv').textContent = d.missingDealerInvoices + d.missingCustomerInvoices;
    document.getElementById('dash-low').textContent = d.lowStock.length;

    document.getElementById('dash-orders').innerHTML = d.recentOrders.map(o => `
      <tr><td>${o.orderNumber}</td><td>${o.dealerName}</td><td>${fmtDate(o.orderDate)}</td>
      <td><span class="badge badge-${o.status}">${statusTR(o.status)}</span></td></tr>`).join('') || '<tr><td colspan="4" class="empty-state"><p>Henüz sipariş yok</p></td></tr>';

    const alerts = [];
    if (d.missingDealerInvoices > 0)
      alerts.push(`<div class="alert-item danger">📄 ${d.missingDealerInvoices} bayi faturası eksik</div>`);
    if (d.missingCustomerInvoices > 0)
      alerts.push(`<div class="alert-item danger">📄 ${d.missingCustomerInvoices} müşteri faturası eksik</div>`);
    d.lowStock.forEach(s =>
      alerts.push(`<div class="alert-item warning">📦 Düşük stok: <strong>${s.productName}</strong> @ ${s.dealerName} (${s.stock} adet kaldı)</div>`));
    document.getElementById('dash-alerts').innerHTML = alerts.join('') || '<p style="color:var(--text-muted)">✅ Uyarı yok</p>';

    const missingTbody = document.getElementById('dash-missing-images');
    if (!d.missingImages || d.missingImages.length === 0) {
      missingTbody.innerHTML = '<tr><td colspan="4" class="empty-state"><p style="color:#10b981">Tüm ürünlerin görseli yüklü!</p></td></tr>';
    } else {
      missingTbody.innerHTML = d.missingImages.map(m => `
          <tr>
            <td><strong>${m.productCode || '-'}</strong></td>
            <td>${m.productName}</td>
            <td style="font-size:12px;color:#64748b">${m.brandName}<br/>${m.catalogName}</td>
            <td><span class="badge badge-info">${m.collectionName}</span></td>
          </tr>
        `).join('');
    }
  } catch (e) {
    toast('Panel istatistikleri yüklenemedi', 'danger');
  }

  // Load pending sales deliveries
  checkPendingDeliveriesWidget();
}

async function checkPendingDeliveriesWidget() {
  const tbody = document.getElementById('dash-pending-deliveries');
  if (!tbody) return;

  try {
    const pending = await api('/sales-shipments/pending-deliveries');
    if (!pending || pending.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4" class="empty-state"><p style="color:#10b981">Tüm teslimatlar tamamlandı, eksik yok!</p></td></tr>';
      return;
    }

    tbody.innerHTML = pending.map(p => `
      <tr>
        <td><strong>${p.orderNumber || '-'}</strong></td>
        <td>${p.productName || '-'}</td>
        <td style="color:#ef4444; font-weight:bold;">${p.missingQuantity}</td>
        <td>${p.expectedDeliveryDate ? fmtDate(p.expectedDeliveryDate) : '-'} <br><span style="font-size:10px; color:#64748b">${p.note || ''}</span></td>
      </tr>
    `).join('');
  } catch (err) {
    tbody.innerHTML = '<tr><td colspan="4" style="color:red">Bekleyen teslimatlar alınamadı.</td></tr>';
  }
}

// Make accessible globally so it can be refreshed from other modules
window.checkPendingDeliveriesWidget = checkPendingDeliveriesWidget;

function statusTR(s) {
  return s === 'preparing' ? 'Hazırlanıyor' : s === 'shipped' ? 'Sevk Edildi' : s === 'delivered' ? 'Teslim Edildi' : s;
}
