// Satışlar — Teklif bağlantı ve "teklifi dönüştür" özelliğiyle tam CRUD

async function renderSales() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Satış Geçmişi</h2>
      <button class="btn btn-primary btn-sm" onclick="openSaleModal()">+ Satış Kaydet</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Müşteri</th><th>Satış Temsilcisi</th><th>Satış Tarihi</th><th>Toplam</th><th>Teklif</th><th>İşlemler</th></tr></thead>
        <tbody id="sales-list"><tr><td colspan="6" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadSales();
}

async function loadSales() {
  try {
    const sales = await api('/sales');
    const tbody = document.getElementById('sales-list');
    if (!sales.length) {
      tbody.innerHTML = '<tr><td colspan="6"><div class="empty-state"><p>Henüz satış kaydı yok. Bir satış kaydedin veya teklifi dönüştürün.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = sales.map(s => `
      <tr>
        <td><strong>${s.customer?.companyName || '—'}</strong></td>
        <td>${s.salesRep ? `${s.salesRep.firstName} ${s.salesRep.lastName}` : '—'}</td>
        <td>${fmtDate(s.saleDate)}</td>
        <td><strong style="color:#1a56db">₺${fmtMoney(s.totalPrice)}</strong></td>
        <td>${s.quotation ? `<span class="badge badge-info">${s.quotation.quotationNumber}</span>` : '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openSaleModal(${JSON.stringify(s).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteSale(${s.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Satışlar yüklenemedi: ' + e.message, 'danger');
  }
}

async function openSaleModal(sale = null) {
  try {
    const [customers, reps, quotations] = await Promise.all([
      api('/customers'), api('/sales-reps'), api('/quotations')
    ]);

    openModal(sale ? 'Satış Düzenle' : 'Satış Kaydet', `
      <form id="sale-form">
        <div class="form-row">
          <div class="form-group">
            <label>Müşteri *</label>
            <select class="form-control" name="customerId" required>
              <option value="">— Müşteri Seçin —</option>
              ${customers.map(c => `<option value="${c.id}" ${sale?.customerId == c.id ? 'selected' : ''}>${c.companyName}</option>`).join('')}
            </select>
          </div>
          <div class="form-group">
            <label>Satış Temsilcisi *</label>
            <select class="form-control" name="salesRepId" required>
              <option value="">— Temsilci Seçin —</option>
              ${reps.map(r => `<option value="${r.id}" ${sale?.salesRepId == r.id ? 'selected' : ''}>${r.firstName} ${r.lastName}</option>`).join('')}
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>Satış Tarihi</label>
            <input class="form-control" name="saleDate" type="date"
              value="${sale ? new Date(sale.saleDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0]}">
          </div>
          <div class="form-group">
            <label>Toplam Tutar *</label>
            <input class="form-control" name="totalPrice" type="number" step="0.01" min="0"
              value="${sale?.totalPrice || ''}" placeholder="0,00" required>
          </div>
        </div>
        <div class="form-group">
          <label>İlgili Teklif <span style="color:#94a3b8">(isteğe bağlı)</span></label>
          <select class="form-control" name="quotationId" id="qt-select" onchange="fillFromQuotation(this, ${JSON.stringify(quotations)})">
            <option value="">— Yok —</option>
            ${quotations.map(q => `<option value="${q.id}" ${sale?.quotationId == q.id ? 'selected' : ''}
              data-total="${q.totalPrice}" data-customer="${q.customerId}" data-rep="${q.salesRepId}">
              ${q.quotationNumber} — ${q.customer?.companyName || ''} (₺${fmtMoney(q.totalPrice)})
            </option>`).join('')}
          </select>
          <p style="font-size:11px;color:#94a3b8;margin-top:4px">Teklif seçildiğinde müşteri, temsilci ve toplam tutar otomatik doldurulur.</p>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${sale ? 'Değişiklikleri Kaydet' : 'Satışı Kaydet'}</button>
        </div>
      </form>`);

    document.getElementById('sale-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = {
        customerId: +fd.get('customerId'),
        salesRepId: +fd.get('salesRepId'),
        saleDate: new Date(fd.get('saleDate')).toISOString(),
        totalPrice: +fd.get('totalPrice'),
        quotationId: fd.get('quotationId') ? +fd.get('quotationId') : null
      };
      try {
        if (sale) {
          await api(`/sales/${sale.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Satış güncellendi', 'success');
        } else {
          await api('/sales', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Satış kaydedildi', 'success');
        }
        closeModal();
        await loadSales();
      } catch (err) {
        toast('Satış kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Satış formu açılamadı: ' + e.message, 'danger');
  }
}

function fillFromQuotation(sel, quotations) {
  const qid = +sel.value;
  if (!qid) return;
  const q = quotations.find(x => x.id === qid);
  if (!q) return;
  const form = document.getElementById('sale-form');
  const custSel = form.querySelector('[name="customerId"]');
  const repSel = form.querySelector('[name="salesRepId"]');
  const totalInput = form.querySelector('[name="totalPrice"]');
  if (custSel) custSel.value = q.customerId;
  if (repSel) repSel.value = q.salesRepId;
  if (totalInput) totalInput.value = q.totalPrice;
}

async function deleteSale(id) {
  if (!confirm('Bu satış kaydını silmek istiyor musunuz?')) return;
  try {
    await api(`/sales/${id}`, { method: 'DELETE' });
    toast('Satış silindi');
    await loadSales();
  } catch (e) {
    toast('Satış silinemedi: ' + e.message, 'danger');
  }
}
