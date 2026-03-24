// Satış Temsilcileri — Logo yükleme ile tam CRUD

async function renderSalesReps() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Satış Temsilcileri</h2>
      <button class="btn btn-primary btn-sm" onclick="openRepModal()">+ Temsilci Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Logo</th><th>Ad Soyad</th><th>Telefon</th><th>E-posta</th><th>İşlemler</th></tr></thead>
        <tbody id="reps-list"><tr><td colspan="5" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadReps();
}

async function loadReps() {
  try {
    const reps = await api('/sales-reps');
    const tbody = document.getElementById('reps-list');
    if (!reps.length) {
      tbody.innerHTML = '<tr><td colspan="5"><div class="empty-state"><p>Henüz satış temsilcisi yok. Başlamak için "+ Temsilci Ekle" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = reps.map(r => `
      <tr>
        <td>${r.logoPath ? `<img src="${r.logoPath}" style="height:40px;width:40px;object-fit:contain;border-radius:6px;border:1px solid #e2e8f0">` : '<span style="color:#cbd5e1;font-size:12px">Logo yok</span>'}</td>
        <td><strong>${r.firstName} ${r.lastName}</strong></td>
        <td>${r.phone || '—'}</td>
        <td>${r.email || '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openRepModal(${JSON.stringify(r).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-danger btn-sm" onclick="deleteRep(${r.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Satış temsilcileri yüklenemedi: ' + e.message, 'danger');
  }
}

function openRepModal(rep = null) {
  openModal(rep ? 'Temsilci Düzenle' : 'Temsilci Ekle', `
    <form id="rep-form">
      <div class="form-row">
        <div class="form-group">
          <label>Ad *</label>
          <input class="form-control" name="firstName" value="${rep?.firstName || ''}" placeholder="Ad" required>
        </div>
        <div class="form-group">
          <label>Soyad *</label>
          <input class="form-control" name="lastName" value="${rep?.lastName || ''}" placeholder="Soyad" required>
        </div>
      </div>
      <div class="form-row">
        <div class="form-group">
          <label>Telefon</label>
          <input class="form-control" name="phone" value="${rep?.phone || ''}" placeholder="+90 555 000 0000">
        </div>
        <div class="form-group">
          <label>E-posta</label>
          <input class="form-control" name="email" type="email" value="${rep?.email || ''}" placeholder="temsilci@sirket.com">
        </div>
      </div>
      <div class="form-group">
        <label>Logo <span style="color:#94a3b8">(PDF tekliflerde kullanılır)</span></label>
        ${rep?.logoPath ? `<div style="margin-bottom:6px"><img src="${rep.logoPath}" style="height:50px;border-radius:6px"></div>` : ''}
        <input type="file" class="form-control" name="logo" accept="image/*">
      </div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary">${rep ? 'Değişiklikleri Kaydet' : 'Temsilci Oluştur'}</button>
      </div>
    </form>`);

  document.getElementById('rep-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fd = new FormData(e.target);
    if (!fd.get('logo')?.size) fd.delete('logo');
    try {
      if (rep) {
        await api(`/sales-reps/${rep.id}`, { method: 'PUT', body: fd });
        toast('Temsilci güncellendi', 'success');
      } else {
        await api('/sales-reps', { method: 'POST', body: fd });
        toast('Temsilci oluşturuldu', 'success');
      }
      closeModal();
      await loadReps();
    } catch (err) {
      toast('Temsilci kaydedilemedi: ' + err.message, 'danger');
    }
  });
}

async function deleteRep(id) {
  if (!confirm('Bu satış temsilcisini silmek istiyor musunuz?')) return;
  try {
    await api(`/sales-reps/${id}`, { method: 'DELETE' });
    toast('Temsilci silindi');
    await loadReps();
  } catch (e) {
    toast('Temsilci silinemedi: ' + e.message, 'danger');
  }
}
