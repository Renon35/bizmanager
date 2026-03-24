// Bayiler — Marka seçimi ve Excel içe aktarma ile tam CRUD

async function renderDealers() {
  document.getElementById('page-content').innerHTML = `
  <div class="card">
    <div class="card-header">
      <h2>Bayiler</h2>
      <button class="btn btn-primary btn-sm" onclick="openDealerModal()">+ Bayi Ekle</button>
    </div>
    <div class="card-body">
      <table>
        <thead><tr><th>Ad</th><th>Marka</th><th>İletişim Kişisi</th><th>Telefon</th><th>E-posta</th><th>Adres</th><th>İşlemler</th></tr></thead>
        <tbody id="dealers-list"><tr><td colspan="7" style="text-align:center;padding:20px;color:#94a3b8">Yükleniyor…</td></tr></tbody>
      </table>
    </div>
  </div>`;
  await loadDealers();
}

async function loadDealers() {
  try {
    const dealers = await api('/dealers');
    const tbody = document.getElementById('dealers-list');
    if (!dealers.length) {
      tbody.innerHTML = '<tr><td colspan="7"><div class="empty-state"><p>Henüz bayi yok. Başlamak için "+ Bayi Ekle" butonuna tıklayın.</p></div></td></tr>';
      return;
    }
    tbody.innerHTML = dealers.map(d => `
      <tr>
        <td><strong>${d.name}</strong></td>
        <td>${d.brand?.name ? `<span class="badge badge-info">${d.brand.name}</span>` : '—'}</td>
        <td>${d.contactPerson || '—'}</td>
        <td>${d.phone || '—'}</td>
        <td>${d.email || '—'}</td>
        <td>${d.address || '—'}</td>
        <td>
          <div class="section-actions">
            <button class="btn btn-ghost btn-sm" onclick='openDealerModal(${JSON.stringify(d).replace(/'/g, "&#39;")})'>Düzenle</button>
            <button class="btn btn-primary btn-sm" onclick="openImportModal(${d.id}, '${d.name.replace(/'/g, "\\'")}')">Excel İçe Aktar</button>
            <button class="btn btn-danger btn-sm" onclick="deleteDealer(${d.id})">Sil</button>
          </div>
        </td>
      </tr>`).join('');
  } catch (e) {
    toast('Bayiler yüklenemedi: ' + e.message, 'danger');
  }
}

async function openDealerModal(dealer = null) {
  try {
    const brands = await api('/brands');
    openModal(dealer ? 'Bayi Düzenle' : 'Bayi Ekle', `
      <form id="dealer-form">
        <div class="form-row">
          <div class="form-group">
            <label>Bayi Adı *</label>
            <input class="form-control" name="name" value="${dealer?.name || ''}" placeholder="Bayi şirket adı" required>
          </div>
          <div class="form-group">
            <label>Marka *</label>
            <select class="form-control" name="brandId" required>
              <option value="">— Marka Seçin —</option>
              ${brands.map(b => `<option value="${b.id}" ${dealer?.brandId == b.id ? 'selected' : ''}>${b.name}</option>`).join('')}
            </select>
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>İletişim Kişisi</label>
            <input class="form-control" name="contactPerson" value="${dealer?.contactPerson || ''}" placeholder="Kişi adı">
          </div>
          <div class="form-group">
            <label>Telefon</label>
            <input class="form-control" name="phone" value="${dealer?.phone || ''}" placeholder="+90 555 000 0000">
          </div>
        </div>
        <div class="form-row">
          <div class="form-group">
            <label>E-posta</label>
            <input class="form-control" name="email" type="email" value="${dealer?.email || ''}" placeholder="iletisim@bayi.com">
          </div>
          <div class="form-group">
            <label>Adres</label>
            <input class="form-control" name="address" value="${dealer?.address || ''}" placeholder="Sokak, Şehir, Ülke">
          </div>
        </div>
        <div class="form-group">
          <label>Notlar</label>
          <textarea class="form-control" name="notes" rows="2" placeholder="İsteğe bağlı notlar">${dealer?.notes || ''}</textarea>
        </div>
        <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
          <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
          <button type="submit" class="btn btn-primary">${dealer ? 'Değişiklikleri Kaydet' : 'Bayi Oluştur'}</button>
        </div>
      </form>`);

    document.getElementById('dealer-form').addEventListener('submit', async e => {
      e.preventDefault();
      const fd = new FormData(e.target);
      const body = {
        name: fd.get('name'), brandId: +fd.get('brandId'),
        contactPerson: fd.get('contactPerson'), phone: fd.get('phone'),
        email: fd.get('email'), address: fd.get('address'), notes: fd.get('notes')
      };
      try {
        if (dealer) {
          await api(`/dealers/${dealer.id}`, { method: 'PUT', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Bayi güncellendi', 'success');
        } else {
          await api('/dealers', { method: 'POST', body: JSON.stringify(body), headers: { 'Content-Type': 'application/json' } });
          toast('Bayi oluşturuldu', 'success');
        }
        closeModal();
        await loadDealers();
      } catch (err) {
        toast('Bayi kaydedilemedi: ' + err.message, 'danger');
      }
    });
  } catch (e) {
    toast('Bayi formu açılamadı: ' + e.message, 'danger');
  }
}

function openImportModal(dealerId, dealerName) {
  openModal(`Excel İçe Aktar — ${dealerName}`, `
    <div style="margin-bottom:12px;padding:10px 14px;background:#f0f9ff;border:1px solid #bae6fd;border-radius:8px;font-size:13px;color:#0369a1">
      <strong>Beklenen Sütunlar (A→H):</strong><br>
      Ürün Kodu | Ürün Adı | Koli Boyutu | Paket Boyutu | Koli Fiyatı | Paket Fiyatı | Birim Fiyatı | Stok
    </div>
    <form id="import-form" enctype="multipart/form-data">
      <div class="form-group">
        <label>Excel Dosyası (.xlsx / .xls) *</label>
        <input class="form-control" id="import-file" name="file" type="file" accept=".xlsx,.xls" required>
      </div>
      <div id="import-result" style="display:none;margin-top:12px;padding:12px;border-radius:8px;background:#f0fdf4;border:1px solid #86efac;font-size:13px;color:#166534"></div>
      <div style="display:flex;gap:8px;justify-content:flex-end;margin-top:16px">
        <button type="button" class="btn btn-ghost" onclick="closeModal()">İptal</button>
        <button type="submit" class="btn btn-primary" id="import-btn">İçe Aktar</button>
      </div>
    </form>`);

  document.getElementById('import-form').addEventListener('submit', async e => {
    e.preventDefault();
    const fileInput = document.getElementById('import-file');
    if (!fileInput.files.length) { toast('Lütfen bir dosya seçin.', 'danger'); return; }

    const btn = document.getElementById('import-btn');
    btn.disabled = true;
    btn.textContent = 'Aktarılıyor…';

    try {
      const formData = new FormData();
      formData.append('file', fileInput.files[0]);

      const resp = await fetch(`/api/import/excel?dealerId=${dealerId}`, {
        method: 'POST',
        body: formData
      });

      if (!resp.ok) {
        const err = await resp.json().catch(() => ({ error: resp.statusText }));
        throw new Error(err.error || resp.statusText);
      }

      const result = await resp.json();
      const resultBox = document.getElementById('import-result');
      resultBox.style.display = 'block';
      resultBox.innerHTML = `
        <strong>✅ İçe aktarma tamamlandı!</strong><br><br>
        <table style="width:100%;border-collapse:collapse">
          <tr><td style="padding:3px 8px">📦 Oluşturulan ürünler</td><td style="font-weight:600">${result.productsCreated}</td></tr>
          <tr><td style="padding:3px 8px">✏️ Güncellenen ürünler</td><td style="font-weight:600">${result.productsUpdated}</td></tr>
          <tr><td style="padding:3px 8px">💰 Güncellenen bayi fiyatları</td><td style="font-weight:600">${result.dealerPricesUpdated}</td></tr>
          <tr><td style="padding:3px 8px">⏭️ Atlanan satırlar</td><td style="font-weight:600">${result.rowsSkipped}</td></tr>
        </table>`;
      toast(`İçe aktarma tamamlandı: ${result.productsCreated} ürün oluşturuldu, ${result.dealerPricesUpdated} fiyat güncellendi`, 'success');
    } catch (err) {
      toast('İçe aktarma başarısız: ' + err.message, 'danger');
      btn.disabled = false;
      btn.textContent = 'İçe Aktar';
    }
  });
}

async function deleteDealer(id) {
  if (!confirm('Bu bayiyi silmek istiyor musunuz?')) return;
  try {
    await api(`/dealers/${id}`, { method: 'DELETE' });
    toast('Bayi silindi');
    await loadDealers();
  } catch (e) {
    toast('Bayi silinemedi: ' + e.message, 'danger');
  }
}
