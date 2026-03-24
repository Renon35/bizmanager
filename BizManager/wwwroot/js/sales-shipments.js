// Satış Sevkiyatları ve Eksik Teslimat Takibi

async function renderSalesShipments() {
  document.getElementById('page-content').innerHTML = `
  <div class="card" style="margin-bottom: 24px;">
    <div class="card-header">
      <h2>Satış Sevkiyatları</h2>
      <button class="btn btn-primary btn-sm" onclick="openCreateShipmentModal()">+ Sevkiyat Oluştur</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Sipariş No</th><th>Müşteri</th><th>Tarih</th><th>Durum</th><th>Kargo/Kurye</th><th>Takip No</th><th>İşlemler</th></tr></thead>
        <tbody id="sales-shipments-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadSalesShipments();
}

async function loadSalesShipments() {
  try {
    const shipments = await api('/sales-shipments');
    const tbody = document.getElementById('sales-shipments-list');
    if (!shipments.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Kayıtlı satış sevkiyatı bulunamadı.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = shipments.map(s => `
      <tr>
        <td><strong>${s.salesOrder?.orderNumber || '—'}</strong></td>
        <td>${s.salesOrder?.customer?.companyName || '—'}</td>
        <td>${fmtDate(s.shipmentDate)}</td>
        <td><span class="badge ${s.status}">${s.status}</span></td>
        <td>${s.shippingCompany || '—'}</td>
        <td>${s.trackingNumber || '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick="viewSalesShipment(${s.id})">Detay</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Sevkiyatlar yüklenemedi: ' + e.message, 'danger');
  }
}

async function openCreateShipmentModal() {
  try {
    // Only load pending or partial orders
    const ordersRaw = await api('/sales-orders');
    const orders = ordersRaw.filter(o => o.status === 'pending' || o.status === 'partial');

    openModal('Yeni Sevkiyat Oluştur', `
      <form id="create-shipment-form">
        <div class="form-group">
          <label>Satış Siparişi *</label>
          <select class="form-control" name="salesOrderId" id="sel-sales-order" required>
            <option value="">— Sipariş Seçin —</option>
            ${orders.map(o => `<option value="${o.id}">${o.orderNumber} - ${o.customer?.companyName}</option>`).join('')}
          </select>
        </div>
        
        <div class="grid" style="grid-template-columns: 1fr 1fr; gap: 16px;">
          <div class="form-group">
            <label>Sevkiyat Tarihi *</label>
            <input type="date" class="form-control" name="shipmentDate" value="${new Date().toISOString().split('T')[0]}" required>
          </div>
          <div class="form-group">
            <label>Kargo Firması</label>
            <input type="text" class="form-control" name="shippingCompany" placeholder="Örn: Aras Kargo">
          </div>
          <div class="form-group">
            <label>Takip Numarası</label>
            <input type="text" class="form-control" name="trackingNumber" placeholder="Takip Kodu">
          </div>
        </div>

        <div id="shipment-items-container" style="margin-top:24px; display:none;">
          <h4 style="margin-bottom:12px; font-size: 14px;">Teslim Edilen Ürünler (Miktarları Girin)</h4>
          <div class="items-table-wrapper">
            <table>
              <thead>
                <tr>
                  <th>Ürün</th>
                  <th style="width:80px">Sipariş</th>
                  <th style="width:100px">Teslim Edilen</th>
                  <th style="width:140px">Eksik İçin Tarih</th>
                  <th>Not</th>
                </tr>
              </thead>
              <tbody id="shipment-items-body"></tbody>
            </table>
          </div>
        </div>

        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px;border-top:1px solid var(--border);padding-top:16px;">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">Sevkiyatı Kaydet</button>
        </div>
      </form>
    `);

    // Listen to order selection to build the items layout
    document.getElementById('sel-sales-order').addEventListener('change', async (e) => {
      const orderId = e.target.value;
      const itemsContainer = document.getElementById('shipment-items-container');
      const itemsBody = document.getElementById('shipment-items-body');
      
      if (!orderId) {
        itemsContainer.style.display = 'none';
        itemsBody.innerHTML = '';
        return;
      }

      try {
        const order = await api(`/sales-orders/${orderId}`);
        // Build items based on what hasn't been completely delivered yet historically.
        // For simplicity in this iteration, we trust the user to look at the order requirements, 
        // but we assume they fill out quantity delivered for this specific shipment batch.
        
        itemsBody.innerHTML = order.items.map(i => `
          <tr class="shipment-product-row" data-product-id="${i.productId}" data-ordered-qty="${i.quantity}">
            <td><div style="font-size:12px; font-weight:600;">${i.product?.productName}</div><div style="font-size:11px; color:var(--text-muted);">${i.product?.productCode || ''}</div></td>
            <td style="text-align:center">${i.quantity}</td>
            <td><input type="number" class="form-control delivered-qty" value="${i.quantity}" min="0"></td>
            <td><input type="date" class="form-control expected-date"></td>
            <td><input type="text" class="form-control missing-note" placeholder="Sebep (Opsiyonel)"></td>
          </tr>
        `).join('');

        itemsContainer.style.display = 'block';

      } catch (err) {
        toast('Sipariş kalemleri alınamadı', 'danger');
      }
    });

    document.getElementById('create-shipment-form').addEventListener('submit', async (e) => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const req = {
        salesOrderId: parseInt(fd.get('salesOrderId')),
        shipmentDate: fd.get('shipmentDate'),
        shippingCompany: fd.get('shippingCompany'),
        trackingNumber: fd.get('trackingNumber'),
        deliveryItems: []
      };

      const rows = document.querySelectorAll('.shipment-product-row');
      rows.forEach(r => {
        const productId = parseInt(r.getAttribute('data-product-id'));
        const orderedQty = parseInt(r.getAttribute('data-ordered-qty'));
        const deliveredQty = parseInt(r.querySelector('.delivered-qty').value || 0);
        
        // Auto calculate missing
        const missing = orderedQty - deliveredQty;
        const expectedDate = r.querySelector('.expected-date').value;
        const note = r.querySelector('.missing-note').value;

        // If they delivered anything, or if they explicitly left it 0, record it
        req.deliveryItems.push({
          productId: productId,
          orderedQuantity: orderedQty,
          deliveredQuantity: deliveredQty,
          expectedDeliveryDate: missing > 0 && expectedDate ? expectedDate : null,
          note: missing > 0 ? note : null
        });
      });

      if (req.deliveryItems.length === 0) return;

      try {
        await api('/sales-shipments', { method: 'POST', body: JSON.stringify(req), headers: {'Content-Type': 'application/json'} });
        toast('Sevkiyat kaydedildi.', 'success');
        closeModal();
        await loadSalesShipments();
        if (typeof checkPendingDeliveriesWidget === 'function') checkPendingDeliveriesWidget();
      } catch (err) {
        toast('Hata: ' + err.message, 'danger');
      }
    });

  } catch (err) {
    toast('Data yüklenemedi: ' + err.message, 'danger');
  }
}

async function viewSalesShipment(id) {
  try {
    const shipment = await api(`/sales-shipments/${id}`);
    
    const itemsHtml = shipment.deliveryItems.map(di => {
      const missingBadge = di.missingQuantity > 0 ? `<span class="badge partial">Eksik: ${di.missingQuantity}</span>` : `<span class="badge complete">Tamam</span>`;
      return `
      <tr>
        <td>${di.product?.productName || '-'}</td>
        <td style="text-align:right">${di.orderedQuantity}</td>
        <td style="text-align:right"><strong>${di.deliveredQuantity}</strong></td>
        <td>${missingBadge}</td>
        <td>${di.expectedDeliveryDate ? fmtDate(di.expectedDeliveryDate) : '-'}</td>
        <td>${di.note || '-'}</td>
        <td>
          ${di.missingQuantity > 0 ? `<button class="btn btn-ghost btn-sm" onclick="promptResolveMissing(${di.id}, ${di.orderedQuantity}, ${di.deliveredQuantity})">Düzenle</button>` : ''}
        </td>
      </tr>
      `;
    }).join('');

    openModal(`Sevkiyat Detayı #${shipment.id}`, `
      <div style="margin-bottom: 24px;">
        <p><strong>Sipariş No:</strong> ${shipment.salesOrder?.orderNumber}</p>
        <p><strong>Tarih:</strong> ${fmtDate(shipment.shipmentDate)}</p>
        <p><strong>Durum:</strong> <span class="badge ${shipment.status}">${shipment.status}</span></p>
        <p><strong>Kargo:</strong> ${shipment.shippingCompany || '-'} (${shipment.trackingNumber || '-'})</p>
      </div>
      <h4 style="margin-bottom:12px; font-size: 14px;">Teslimat Kalemleri (Eksik Takibi)</h4>
      <div class="items-table-wrapper" style="margin-bottom: 24px;">
        <table>
          <thead>
            <tr>
              <th>Ürün</th><th style="text-align:right">Sipariş</th><th style="text-align:right">Teslim</th>
              <th>Durum</th><th>Beklenen Tarih</th><th>Not</th><th>İşlem</th>
            </tr>
          </thead>
          <tbody>${itemsHtml}</tbody>
        </table>
      </div>
      <div style="display:flex; justify-content:flex-end;">
        <button class="btn btn-ghost" onclick="closeModal()">Kapat</button>
      </div>
    `);
  } catch (err) {
    toast('Sevkiyat yüklenemedi: ' + err.message, 'danger');
  }
}

async function promptResolveMissing(deliveryItemId, ordered, delivered) {
  const missing = ordered - delivered;
  // Simplified manual prompt to resolve missing items (e.g., they receive the rest).
  const qtyStr = prompt(`Şu anda ${missing} adet eksik görünüyor.\nToplam teslim edilen adeti güncelleyin (Eski: ${delivered}, Sipariş Edilen: ${ordered}):`, ordered);
  if (qtyStr === null) return;
  const newDelivered = parseInt(qtyStr);
  if (isNaN(newDelivered)) return;

  try {
    await api(`/sales-shipments/delivery-item/${deliveryItemId}`, {
      method: 'PUT',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ deliveredQuantity: newDelivered })
    });
    toast('Eksik adet güncellendi.', 'success');
    closeModal();
    // Re-open details
    await loadSalesShipments();
    if (typeof checkPendingDeliveriesWidget === 'function') checkPendingDeliveriesWidget();
  } catch (e) {
    toast('Güncellenemedi: ' + e.message, 'danger');
  }
}
