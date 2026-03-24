// Sevkiyatlar — Satın alma siparişlerine bağlı tam CRUD

async function renderShipments() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Sevkiyat Takibi</h2>
      <button class="btn btn-primary btn-sm" onclick="openShipmentModal()">+ Sevkiyat Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Sipariş #</th><th>Bayi</th><th>Taşiyici</th><th>Takip #</th><th>Sevk Tarihi</th><th>Teslimat Durumu</th><th>İşlemler</th></tr></thead>
        <tbody id="shipments-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadShipments();
}

async function loadShipments() {
  try {
    const shipments = await api('/shipments');
    const tbody = document.getElementById('shipments-list');
    if (!shipments.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Henüz sevkiyat yok. Siparişten sonra sevkiyat bilgisi ekleyin.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = shipments.map(s => `
      <tr>
        <td><strong>${s.order?.orderNumber || '—'}</strong></td>
        <td>${s.order?.dealer?.name || '—'}</td>
        <td>${s.shippingCompany || '—'}</td>
        <td>${s.trackingNumber ? `<code style="background:#f1f5f9;padding:2px 6px;border-radius:4px">${s.trackingNumber}</code>` : '—'}</td>
        <td>${fmtDate(s.shipmentDate)}</td>
        <td>${s.deliveryStatus ? `<span class="badge badge-shipped">${s.deliveryStatus}</span>` : '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openShipmentModal(${JSON.stringify(s).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteShipment(${s.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Sevkiyatlar yüklenemedi: ' + e.message, 'danger');
  }
}

async function openShipmentModal(shipment = null) {
  try {
    const orders = await api('/purchase-orders');
    openModal(shipment ? 'Sevkiyat Düzenle' : 'Sevkiyat Ekle', `
      <form id="shipment-form">
        <div class="form-group">
          <label>Satın Alma Siparişi *</label>
          <select class="form-control" name="orderId" required>
            <option value="">— Sipariş Seçin —</option>
            ${orders.map(o => `<option value="${o.id}" ${shipment?.orderId == o.id ? 'selected' : ''}>${o.orderNumber}${o.dealer ? ' — ' + o.dealer.name : ''}</option>`).join('')}
          </select>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Nakliye Şirketi</label>
            <input class="form-control" name="shippingCompany" value="${shipment?.shippingCompany || ''}" placeholder="ör. DHL, FedEx, UPS">
          </div>
          <div class="form-group">
            <label>Takip Numarası</label>
            <input class="form-control" name="trackingNumber" value="${shipment?.trackingNumber || ''}" placeholder="Kargo takip numarası">
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Sevk Tarihi</label>
            <input class="form-control" name="shipmentDate" type="date" value="${shipment?.shipmentDate ? new Date(shipment.shipmentDate).toISOString().split('T')[0] : ''}">
          </div>
          <div class="form-group">
            <label>Teslimat Durumu</label>
            <input class="form-control" name="deliveryStatus" value="${shipment?.deliveryStatus || ''}" placeholder="ör. Yolda, Teslim Edildi">
          </div>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${shipment ? 'Değişiklikleri Kaydet' : 'Sevkiyat Oluştur'}</button>
        </div>
      </form>`);

    document.getElementById('shipment-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = {
        orderId: +fd.get('orderId'),
        shippingCompany: fd.get('shippingCompany') || null,
        trackingNumber: fd.get('trackingNumber') || null,
        shipmentDate: fd.get('shipmentDate') ? new Date(fd.get('shipmentDate')).toISOString() : null,
        deliveryStatus: fd.get('deliveryStatus') || null
      };
      try {
        if (shipment) {
          await api(`/shipments/${shipment.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Sevkiyat güncellendi', 'success');
        } else {
          await api('/shipments', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Sevkiyat oluşturuldu', 'success');
        }
        closeModal();
        await loadShipments();
      } catch (err) {
        toast('Sevkiyat kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Sevkiyat formu açılamadı: ' + e.message, 'danger');
  }
}

async function deleteShipment(id) {
  if (!confirm('Bu sevkiyat kaydını silmek istiyor musunuz?')) return;
  try {
    await api(`/shipments/${id}`, { method: 'DELETE' });
    toast('Sevkiyat silindi');
    await loadShipments();
  } catch (e) {
    toast('Sevkiyat silinemedi: ' + e.message, 'danger');
  }
}
