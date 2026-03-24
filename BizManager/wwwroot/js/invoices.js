// Faturalar — Dosya yükleme ile sekmeli bayi ve müşteri faturaları

async function renderInvoices() {
  document.getElementById('page-content').innerHTML = `
    <div class="tabs">
      <button class="tab active" id="tab-dealer" onclick="switchInvTab('dealer')">📦 Bayi Faturaları</button>
      <button class="tab" id="tab-customer" onclick="switchInvTab('customer')">🏢 Müşteri Faturaları</button>
    </div>
    <div id="inv-content"></div>`;
  await renderDealerInvoices();
}

async function switchInvTab(tab) {
  document.querySelectorAll('.tab').forEach(t => t.classList.remove('active'));
  document.getElementById(`tab-${tab}`).classList.add('active');
  if (tab === 'dealer') await renderDealerInvoices();
  else await renderCustomerInvoices();
}

// ─── BAYİ FATURALARI ──────────────────────────────────────────────────────────

async function renderDealerInvoices() {
  try {
    const [inv, orders] = await Promise.all([api('/invoices/dealer'), api('/purchase-orders')]);
    window._dinvOrders = orders;

    document.getElementById('inv-content').innerHTML = `
      <div class="card">
        <div class="card-header">
          <h2>Bayi Faturaları</h2>
          <button class="btn btn-primary btn-sm" onclick="openDealerInvModal()">+ Bayi Faturası Ekle</button>
        </div>
        <div class="card-body">
          <table>
            <thead><tr><th>Sipariş #</th><th>Bayi</th><th>Fatura #</th><th>Fatura Tarihi</th><th>Durum</th><th>Dosya</th><th>İşlemler</th></tr></thead>
            <tbody id="dinv-list"></tbody>
          </table>
        </div>
      </div>`;

    document.getElementById('dinv-list').innerHTML = inv.length
      ? inv.map(i => `
        <tr>
          <td><strong>${i.order?.orderNumber || '—'}</strong></td>
          <td>${i.order?.dealer?.name || '—'}</td>
          <td>${i.invoiceNumber || '—'}</td>
          <td>${fmtDate(i.invoiceDate)}</td>
          <td><span class="badge ${i.issued ? 'badge-issued' : 'badge-missing'}">${i.issued ? '✓ Kesildi' : '✗ Eksik'}</span></td>
          <td>${i.filePath ? `<a href="${i.filePath}" target="_blank" class="btn btn-ghost btn-sm">📎 Görüntüle</a>` : '—'}</td>
          <td>
            <div class="section-actions">
              <button class="btn btn-ghost btn-sm" onclick='openDealerInvModal(${JSON.stringify(i).replace(/'/g, "&#39;")})'>Düzenle</button>
              <button class="btn btn-danger btn-sm" onclick="deleteDealerInv(${i.id})">Sil</button>
            </div>
          </td>
        </tr>`).join('')
      : '<tr><td colspan="7"><div class="empty-state"><p>Henüz bayi faturası yok.</p></div></td></tr>';
  } catch (e) {
    toast('Bayi faturaları yüklenemedi: ' + e.message, 'danger');
  }
}

function openDealerInvModal(inv = null) {
  const orders = window._dinvOrders || [];
  openModal(inv ? 'Bayi Faturasını Düzenle' : 'Bayi Faturası Ekle', `
    <form id="dinv-form">
      <div class="form-group">
        <label>Satın Alma Siparişi *</label>
        <select class="form-control" name="orderId" required>
          <option value="">— Sipariş Seçin —</option>
          ${orders.map(o => `<option value="${o.id}" ${inv?.orderId == o.id ? 'selected' : ''}>${o.orderNumber}${o.dealer ? ' — ' + o.dealer.name : ''}</option>`).join('')}
        </select>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Fatura Numarası</label>
          <input class="form-control" name="invoiceNumber" value="${inv?.invoiceNumber || ''}" placeholder="FAT-2026-001">
        </div>
        <div class="form-group">
          <label>Fatura Tarihi</label>
          <input class="form-control" name="invoiceDate" type="date"
            value="${inv?.invoiceDate ? new Date(inv.invoiceDate).toISOString().split('T')[0] : ''}">
        </div>
      </div>
      <div class="form-group">
        <label>Fatura Dosyası (PDF / Görsel)</label>
        ${inv?.filePath ? `<div style="margin-bottom:6px"><a href="${inv.filePath}" target="_blank" class="btn btn-ghost btn-sm">📎 Mevcut dosya</a></div>` : ''}
        <input type="file" class="form-control" name="file" accept=".pdf,.png,.jpg,.jpeg">
      </div>
      <div class="form-group">
        <label style="display:flex;align-items:center;gap:10px;cursor:pointer">
          <input type="checkbox" name="issued" ${inv?.issued ? 'checked' : ''} style="width:16px;height:16px">
          <span>Fatura kesildi / alındı</span>
        </label>
      </div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary">${inv ? 'Değişiklikleri Kaydet' : 'Fatura Oluştur'}</button>
      </div>
    </form>`);

  document.getElementById('dinv-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(e.target);
    if (!fd.get('file')?.size) fd.delete('file');
    fd.set('issued', document.querySelector('#dinv-form [name="issued"]').checked ? 'true' : 'false');
    try {
      if (inv) {
        await api(`/invoices/dealer/${inv.id}`, { method: 'PUT', body: fd });
        toast('Bayi faturası güncellendi', 'success');
      } else {
        await api('/invoices/dealer', { method: 'POST', body: fd });
        toast('Bayi faturası oluşturuldu', 'success');
      }
      closeModal();
      await renderDealerInvoices();
    } catch (err) {
      toast('Fatura kaydedilemedi: ' + err.message, 'danger');
    }
  });
}

async function deleteDealerInv(id) {
  if (!confirm('Bu bayi faturasını silmek istiyor musunuz?')) return;
  try {
    await api(`/invoices/dealer/${id}`, { method: 'DELETE' });
    toast('Bayi faturası silindi');
    await renderDealerInvoices();
  } catch (e) {
    toast('Bayi faturası silinemedi: ' + e.message, 'danger');
  }
}

// ─── MÜŞTERİ FATURALARI ────────────────────────────────────────────────────────

async function renderCustomerInvoices() {
  try {
    const [inv, sales] = await Promise.all([api('/invoices/customer'), api('/sales')]);
    window._cinvSales = sales;

    document.getElementById('inv-content').innerHTML = `
      <div class="card">
        <div class="card-header">
          <h2>Müşteri Faturaları</h2>
          <button class="btn btn-primary btn-sm" onclick="openCustomerInvModal()">+ Müşteri Faturası Ekle</button>
        </div>
        <div class="card-body">
          <table>
            <thead><tr><th>Müşteri</th><th>Fatura #</th><th>Fatura Tarihi</th><th>Durum</th><th>Dosya</th><th>İşlemler</th></tr></thead>
            <tbody id="cinv-list"></tbody>
          </table>
        </div>
      </div>`;

    document.getElementById('cinv-list').innerHTML = inv.length
      ? inv.map(i => `
        <tr>
          <td><strong>${i.sale?.customer?.companyName || '—'}</strong></td>
          <td>${i.invoiceNumber || '—'}</td>
          <td>${fmtDate(i.invoiceDate)}</td>
          <td><span class="badge ${i.issued ? 'badge-issued' : 'badge-missing'}">${i.issued ? '✓ Kesildi' : '✗ Eksik'}</span></td>
          <td>${i.filePath ? `<a href="${i.filePath}" target="_blank" class="btn btn-ghost btn-sm">📎 Görüntüle</a>` : '—'}</td>
          <td>
            <div class="section-actions">
              <button class="btn btn-ghost btn-sm" onclick='openCustomerInvModal(${JSON.stringify(i).replace(/'/g, "&#39;")})'>Düzenle</button>
              <button class="btn btn-danger btn-sm" onclick="deleteCustomerInv(${i.id})">Sil</button>
            </div>
          </td>
        </tr>`).join('')
      : '<tr><td colspan="6"><div class="empty-state"><p>Henüz müşteri faturası yok.</p></div></td></tr>';
  } catch (e) {
    toast('Müşteri faturaları yüklenemedi: ' + e.message, 'danger');
  }
}

function openCustomerInvModal(inv = null) {
  const sales = window._cinvSales || [];
  openModal(inv ? 'Müşteri Faturasını Düzenle' : 'Müşteri Faturası Ekle', `
    <form id="cinv-form">
      <div class="form-group">
        <label>Satış *</label>
        <select class="form-control" name="saleId" required>
          <option value="">— Satış Seçin —</option>
          ${sales.map(s => `<option value="${s.id}" ${inv?.saleId == s.id ? 'selected' : ''}>${s.customer?.companyName || 'Satış #' + s.id} — ${fmtDate(s.saleDate)} (₺${fmtMoney(s.totalPrice)})</option>`).join('')}
        </select>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Fatura Numarası</label>
          <input class="form-control" name="invoiceNumber" value="${inv?.invoiceNumber || ''}" placeholder="MUS-FAT-001">
        </div>
        <div class="form-group">
          <label>Fatura Tarihi</label>
          <input class="form-control" name="invoiceDate" type="date"
            value="${inv?.invoiceDate ? new Date(inv.invoiceDate).toISOString().split('T')[0] : ''}">
        </div>
      </div>
      <div class="form-group">
        <label>Fatura Dosyası (PDF / Görsel)</label>
        ${inv?.filePath ? `<div style="margin-bottom:6px"><a href="${inv.filePath}" target="_blank" class="btn btn-ghost btn-sm">📎 Mevcut dosya</a></div>` : ''}
        <input type="file" class="form-control" name="file" accept=".pdf,.png,.jpg,.jpeg">
      </div>
      <div class="form-group">
        <label style="display:flex;align-items:center;gap:10px;cursor:pointer">
          <input type="checkbox" name="issued" ${inv?.issued ? 'checked' : ''} style="width:16px;height:16px">
          <span>Fatura kesildi / müşteriye gönderildi</span>
        </label>
      </div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary">${inv ? 'Değişiklikleri Kaydet' : 'Fatura Oluştur'}</button>
      </div>
    </form>`);

  document.getElementById('cinv-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(e.target);
    if (!fd.get('file')?.size) fd.delete('file');
    fd.set('issued', document.querySelector('#cinv-form [name="issued"]').checked ? 'true' : 'false');
    try {
      if (inv) {
        await api(`/invoices/customer/${inv.id}`, { method: 'PUT', body: fd });
        toast('Müşteri faturası güncellendi', 'success');
      } else {
        await api('/invoices/customer', { method: 'POST', body: fd });
        toast('Müşteri faturası oluşturuldu', 'success');
      }
      closeModal();
      await renderCustomerInvoices();
    } catch (err) {
      toast('Fatura kaydedilemedi: ' + err.message, 'danger');
    }
  });
}

async function deleteCustomerInv(id) {
  if (!confirm('Bu müşteri faturasını silmek istiyor musunuz?')) return;
  try {
    await api(`/invoices/customer/${id}`, { method: 'DELETE' });
    toast('Müşteri faturası silindi');
    await renderCustomerInvoices();
  } catch (e) {
    toast('Müşteri faturası silinemedi: ' + e.message, 'danger');
  }
}
