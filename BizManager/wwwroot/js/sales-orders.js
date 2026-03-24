// Satış Siparişleri Yönetimi

async function renderSalesOrders() {
  document.getElementById('page-content').innerHTML = `
  <div class="card" style="margin-bottom: 24px;">
    <div class="card-header">
      <h2>Satış Siparişleri</h2>
      <button class="btn btn-primary btn-sm" onclick="openSalesOrderModal()">+ Yeni Sipariş</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Sipariş No</th><th>Müşteri</th><th>Temsilci</th><th>Tarih</th><th>Durum</th><th>İşlemler</th></tr></thead>
        <tbody id="sales-orders-list"><tr><td colspan="6" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadSalesOrders();
}

async function loadSalesOrders() {
  try {
    const orders = await api('/sales-orders');
    const tbody = document.getElementById('sales-orders-list');
    if (!orders.length) {
      tbody.innerHTML = '<tr><td colspan="6"><div class="empty-state"><p>Kayıtlı satış siparişi bulunamadı.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = orders.map(o => `
      <tr>
        <td><strong>${o.orderNumber || '—'}</strong></td>
        <td>${o.customer?.companyName || 'Bilinmiyor'}</td>
        <td>${o.salesRep ? (o.salesRep.firstName + ' ' + o.salesRep.lastName) : '—'}</td>
        <td>${fmtDate(o.orderDate)}</td>
        <td><span class="badge ${o.status}">${o.status}</span></td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick="viewSalesOrder(${o.id})">Görüntüle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteSalesOrder(${o.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Satış siparişleri yüklenemedi: ' + e.message, 'danger');
  }
}

async function openSalesOrderModal(importQId = null) {
  try {
    const customers = await api('/customers');
    const reps = await api('/sales-reps');
    const products = await api('/products');
    const quotations = await api('/quotations');

    openModal('Yeni Satış Siparişi', `
      <form id="sales-order-form">
        <div class="grid" style="grid-template-columns: 1fr 1fr; gap: 16px; margin-bottom: 24px;">
          <div class="form-group" style="grid-column: span 2;">
            <label>Tekliften Aktar (Opsiyonel)</label>
            <select class="form-control" id="so-import-quotation">
              <option value="">— Teklif Seçin —</option>
              ${quotations.map(q => `<option value="${q.id}" ${importQId == q.id ? 'selected' : ''}>${q.quotationNumber} (${q.customer?.companyName || 'Bilinmiyor'})</option>`).join('')}
            </select>
            <small style="color: #64748b">Bir teklif seçerek müşteri, temsilci ve kalemleri otomatik doldurabilirsiniz.</small>
          </div>
          <div class="form-group">
            <label>Sipariş Numarası *</label>
            <input class="form-control" name="orderNumber" value="SO-${Date.now().toString().slice(-6)}" required>
          </div>
          <div class="form-group">
            <label>Tarih *</label>
            <input type="date" class="form-control" name="orderDate" value="${new Date().toISOString().split('T')[0]}" required>
          </div>
          <div class="form-group">
            <label>Müşteri *</label>
            <select class="form-control" name="customerId" required>
              <option value="">— Müşteri Seçin —</option>
              ${customers.map(c => `<option value="${c.id}">${c.companyName}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Satış Temsilcisi *</label>
            <select class="form-control" name="salesRepId" required>
              <option value="">— Temsilci Seçin —</option>
              ${reps.map(r => `<option value="${r.id}">${r.firstName} ${r.lastName}</option>`).join('')}
            </select>
          </div>
        </div>

        <div style="margin-bottom: 16px; display: flex; justify-content: space-between; align-items: center;">
          <h4 style="margin: 0; font-size: 14px;">Sipariş Kalemleri</h4>
          <button type="button" class="btn btn-secondary btn-sm" onclick="addSalesOrderItemRow()">+ Kalem Ekle</button>
        </div>
        
        <div id="sales-order-items-container" style="display: flex; flex-direction: column; gap: 12px; margin-bottom: 24px;"></div>
        
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px;border-top:1px solid var(--border);padding-top:16px;">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">Siparişi Kaydet</button>
        </div>
      </form>`);

      // Add a hidden product list stringified for row creation
      window.__soProducts = products;
      
      const importTrigger = async (qId) => {
        if (!qId) return;
        try {
          const q = await api(`/quotations/${qId}`);
          const form = document.getElementById('sales-order-form');
          if (q.customerId) form.querySelector('[name="customerId"]').value = q.customerId;
          if (q.salesRepId) form.querySelector('[name="salesRepId"]').value = q.salesRepId;
          const container = document.getElementById('sales-order-items-container');
          container.innerHTML = '';
          for (const i of q.items) {
            let matchedProduct = products.find(p => p.productCode === i.productCode);
            if (!matchedProduct) matchedProduct = products.find(p => p.productName === i.productName);
            addSalesOrderItemRow({
              productId: matchedProduct?.id || '',
              quantity: i.quantity,
              unitPrice: i.unitPrice
            });
          }
          toast('Teklif verileri başarıyla aktarıldı', 'success');
        } catch (err) {
          toast('Teklif aktarılamadı: ' + err.message, 'danger');
        }
      };

      // Quotation Import Logic
      document.getElementById('so-import-quotation').addEventListener('change', e => importTrigger(e.target.value));

      if (importQId) {
        await importTrigger(importQId);
      } else {
        addSalesOrderItemRow();
      }

      document.getElementById('sales-order-form').addEventListener('submit', async e => {
        e.preventDefault();
        const fd = new FormData(e.target);
        
        const req = {
          orderNumber: fd.get('orderNumber'),
          customerId: parseInt(fd.get('customerId')),
          salesRepId: parseInt(fd.get('salesRepId')),
          orderDate: fd.get('orderDate'),
          items: []
        };

        const itemRows = document.querySelectorAll('.so-item-row');
        itemRows.forEach(row => {
          const productId = row.querySelector('[name="productId"]').value;
          const qty = row.querySelector('[name="quantity"]').value;
          const price = row.querySelector('[name="unitPrice"]').value;
          if (productId && qty && price) {
            req.items.push({
              productId: parseInt(productId),
              quantity: parseInt(qty),
              unitPrice: parseFloat(price)
            });
          }
        });

        if (req.items.length === 0) {
          toast('En az bir sipariş kalemi eklemelisiniz', 'warning');
          return;
        }

        try {
          await api('/sales-orders', { method: 'POST', body: JSON.stringify(req), headers: {'Content-Type': 'application/json'} });
          toast('Satış siparişi oluşturuldu', 'success');
          closeModal();
          await loadSalesOrders();
        } catch (err) {
          toast('Hata: ' + err.message, 'danger');
        }
      });
  } catch (e) {
    toast('Gerekli veriler yüklenemedi: ' + e.message, 'danger');
  }
}

function addSalesOrderItemRow(data = null) {
  const container = document.getElementById('sales-order-items-container');
  const products = window.__soProducts || [];
  
  const div = document.createElement('div');
  div.className = 'so-item-row grid';
  div.style.gridTemplateColumns = '2fr 1fr 1fr 40px';
  div.style.gap = '8px';
  div.style.alignItems = 'end';
  
  div.innerHTML = `
    <div class="form-group" style="margin-bottom:0">
      <label style="font-size:11px">Ürün</label>
      <select class="form-control" name="productId" required>
        <option value="">— Ürün —</option>
        ${products.map(p => `<option value="${p.id}" ${data && data.productId == p.id ? 'selected' : ''}>${p.productCode ? p.productCode + ' - ' : ''}${p.productName}</option>`).join('')}
      </select>
    </div>
    <div class="form-group" style="margin-bottom:0">
      <label style="font-size:11px">Miktar</label>
      <input type="number" class="form-control" name="quantity" min="1" value="${data ? data.quantity : 1}" required>
    </div>
    <div class="form-group" style="margin-bottom:0">
      <label style="font-size:11px">Birim Fiyat</label>
      <input type="number" class="form-control" name="unitPrice" step="0.01" value="${data ? data.unitPrice.toFixed(2) : '0.00'}" required>
    </div>
    <button type="button" class="btn btn-ghost btn-sm" style="height:36px;padding:0;color:#ef4444" onclick="this.parentElement.remove()">
      <svg viewBox="0 0 24 24" style="width:16px;height:16px"><line x1="18" y1="6" x2="6" y2="18"/><line x1="6" y1="6" x2="18" y2="18"/></svg>
    </button>
  `;
  container.appendChild(div);
}

async function viewSalesOrder(id) {
  try {
    const order = await api(`/sales-orders/${id}`);
    const itemsHtml = order.items.map(i => `
      <tr>
        <td>${i.product?.productCode || '-'}</td>
        <td>${i.product?.productName || '-'}</td>
        <td style="text-align:right">${i.quantity}</td>
        <td style="text-align:right">${fmtCur(i.unitPrice)}</td>
        <td style="text-align:right">${fmtCur(i.totalPrice)}</td>
      </tr>
    `).join('');

    const statusBadge = `<span class="badge ${order.status}">${order.status}</span>`;

    openModal(`Sipariş Detayı: ${order.orderNumber}`, `
      <div style="margin-bottom: 24px;">
        <p><strong>Müşteri:</strong> ${order.customer?.companyName || '-'}</p>
        <p><strong>Temsilci:</strong> ${order.salesRep ? order.salesRep.firstName + ' ' + order.salesRep.lastName : '-'}</p>
        <p><strong>Tarih:</strong> ${fmtDate(order.orderDate)}</p>
        <p><strong>Durum:</strong> ${statusBadge}</p>
      </div>
      <h4 style="margin-bottom:12px; font-size: 14px;">Sipariş Kalemleri</h4>
      <div class="items-table-wrapper" style="margin-bottom: 24px;">
        <table>
          <thead>
            <tr><th>Kod</th><th>Ürün</th><th style="text-align:right">Miktar</th><th style="text-align:right">Fiyat</th><th style="text-align:right">Toplam</th></tr>
          </thead>
          <tbody>${itemsHtml}</tbody>
        </table>
      </div>
      <div style="display:flex; justify-content:flex-end;">
        <button class="btn btn-ghost" onclick="closeModal()">Kapat</button>
      </div>
    `);
  } catch (err) {
    toast('Sipariş yüklenemedi: ' + err.message, 'danger');
  }
}

async function deleteSalesOrder(id) {
  if (!confirm('Bu siparişi silmek istediğinize emin misiniz? İlgili sevkiyatlar da silinecektir.')) return;
  try {
    await api(`/sales-orders/${id}`, { method: 'DELETE' });
    toast('Satış siparişi silindi', 'success');
    await loadSalesOrders();
  } catch (e) {
    toast('Silinemedi: ' + e.message, 'danger');
  }
}
