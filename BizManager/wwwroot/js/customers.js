// Müşteriler — Tam CRUD

async function renderCustomers() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Müşteriler</h2>
      <button class="btn btn-primary btn-sm" onclick="openCustomerModal()">+ Müşteri Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Şirket</th><th>Yetkili</th><th>Telefon</th><th>E-posta</th><th>Vergi No</th><th>Adres</th><th>İşlemler</th></tr></thead>
        <tbody id="customers-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadCustomers();
}

async function loadCustomers() {
  try {
    const customers = await api('/customers');
    const tbody = document.getElementById('customers-list');
    if (!customers.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Henüz müşteri yok. Başlamak için "+ Müşteri Ekle" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = customers.map(c => `
      <tr>
        <td><strong>${c.companyName}</strong></td>
        <td>${c.representative || '—'}</td>
        <td>${c.phone || '—'}</td>
        <td>${c.email || '—'}</td>
        <td>${c.taxNumber ? `<code style="background:#f1f5f9;padding:2px 6px;border-radius:4px">${c.taxNumber}</code>` : '—'}</td>
        <td style="max-width:160px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis">${c.address || '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openCustomerModal(${JSON.stringify(c).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteCustomer(${c.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Müşteriler yüklenemedi: ' + e.message, 'danger');
  }
}

function openCustomerModal(customer = null) {
  openModal(customer ? 'Müşteri Düzenle' : 'Müşteri Ekle', `
    <form id="customer-form">
      <div class="form-row">
        <div class="form-group">
          <label>Şirket Adı *</label>
          <input class="form-control" name="companyName" value="${customer?.companyName || ''}" placeholder="Şirket A.Ş." required>
        </div>
        <div class="form-group">
          <label>Yetkili</label>
          <input class="form-control" name="representative" value="${customer?.representative || ''}" placeholder="İletişim kişisi adı">
        </div>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Telefon</label>
          <input class="form-control" name="phone" value="${customer?.phone || ''}" placeholder="+90 555 000 0000">
        </div>
        <div class="form-group">
          <label>E-posta</label>
          <input class="form-control" name="email" type="email" value="${customer?.email || ''}" placeholder="info@sirket.com">
        </div>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Vergi Numarası</label>
          <input class="form-control" name="taxNumber" value="${customer?.taxNumber || ''}" placeholder="Vergi / KDV numarası">
        </div>
        <div class="form-group">
          <label>Adres</label>
          <input class="form-control" name="address" value="${customer?.address || ''}" placeholder="Sokak, Şehir, Ülke">
        </div>
      </div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary">${customer ? 'Değişiklikleri Kaydet' : 'Müşteri Oluştur'}</button>
      </div>
    </form>`);

  document.getElementById('customer-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(e.target);
    const body = {
      companyName: fd.get('companyName'), representative: fd.get('representative'),
      phone: fd.get('phone'), email: fd.get('email'),
      taxNumber: fd.get('taxNumber'), address: fd.get('address')
    };
    try {
      if (customer) {
        await api(`/customers/${customer.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
        toast('Müşteri güncellendi', 'success');
      } else {
        await api('/customers', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
        toast('Müşteri oluşturuldu', 'success');
      }
      closeModal();
      await loadCustomers();
    } catch (err) {
      toast('Müşteri kaydedilemedi: ' + err.message, 'danger');
    }
  });
}

async function deleteCustomer(id) {
  if (!confirm('Bu müşteriyi silmek istiyor musunuz?')) return;
  try {
    await api(`/customers/${id}`, { method: 'DELETE' });
    toast('Müşteri silindi');
    await loadCustomers();
  } catch (e) {
    toast('Müşteri silinemedi: ' + e.message, 'danger');
  }
}
