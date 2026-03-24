const CatalogAnalysisController = {
    _uploadedFiles: [],
    _analysisResults: null,
    _catalogs: [],
    _brands: [],

    async render() {
        const main = document.getElementById('page-content');
        main.innerHTML = `
      <div class="card" style="margin-bottom:20px;">
        <h2>PDF Katalog Yükleme</h2>
        
        <div class="form-group" style="margin-bottom: 20px; max-width: 400px; margin-top:20px;">
            <label>Marka Seçimi (Şart)</label>
            <select id="ca-brand-select" class="form-control" required>
                <option value="">Marka Seçiniz...</option>
            </select>
            <small class="text-muted">PDF analizi markanın kod yapısına (Örn: Çift Kod) göre işlenir.</small>
        </div>

        <div class="form-group" style="padding: 20px; border: 2px dashed #ccc; border-radius: 8px; text-align: center; margin-top: 10px;" id="ca-drop-zone">
            <p>PDF dosyalarınızı buraya sürükleyin veya seçin</p>
            <input type="file" id="ca-file-input" multiple accept=".pdf" style="display:none;" />
            <button class="btn btn-outline" id="ca-btn-select">Dosya Seç</button>
        </div>
        
        <div id="ca-queue-container" class="hidden" style="margin-top: 20px;">
            <h3>İşlem Sırası</h3>
            <ul id="ca-queue-list" style="list-style: none; padding: 0; margin-top: 10px;"></ul>
            <div style="margin-top: 15px;">
                <button class="btn btn-primary" id="ca-btn-analyze">Analizi Başlat</button>
            </div>
        </div>
      </div>

      <div id="ca-review-card" class="card hidden">
        <h2>Analiz Önizleme <small id="ca-review-filename" style="color:#666; font-weight:normal; margin-left:10px;"></small></h2>
        
        <div class="form-group" style="margin-bottom: 20px; max-width: 400px;">
            <label>Hedef Katalog</label>
            <select id="ca-catalog-select" class="form-control">
                <option value="">Seçiniz</option>
            </select>
        </div>

        <div class="table-responsive">
          <table class="table" id="ca-review-table">
            <thead>
              <tr>
                <th>Görsel</th>
                <th>Kalıp Kodu</th>
                <th>Ürün Kodu</th>
                <th>Ürün Adı</th>
                <th>Koleksiyon</th>
                <th>Koli İçi</th>
                <th>Sayfa</th>
              </tr>
            </thead>
            <tbody id="ca-review-tbody"></tbody>
          </table>
        </div>
        
        <div style="margin-top: 20px; display:flex; justify-content: flex-end;">
            <button class="btn btn-primary" id="ca-btn-commit">Sisteme Kaydet</button>
        </div>
      </div>
    `;

        document.getElementById('page-title').textContent = 'Katalog Analizi (PDF)';

        // Fetch Catalogs and Brands for dropdowns
        try {
            const [catRes, brandRes] = await Promise.all([
                fetch('/api/catalogs'),
                fetch('/api/brands')
            ]);
            if (catRes.ok) this._catalogs = await catRes.json();
            if (brandRes.ok) this._brands = await brandRes.json();
        } catch { }

        const catalogSelect = document.getElementById('ca-catalog-select');
        this._catalogs.forEach(c => {
            const opt = document.createElement('option');
            opt.value = c.id;
            opt.textContent = `${c.brand?.name || 'Markasız'} - ${c.catalogName}`;
            catalogSelect.appendChild(opt);
        });

        const brandSelect = document.getElementById('ca-brand-select');
        this._brands.forEach(b => {
            const opt = document.createElement('option');
            opt.value = b.id;
            opt.textContent = `${b.name} (${b.codeStructure})`;
            brandSelect.appendChild(opt);
        });

        this.bindEvents();
    },

    bindEvents() {
        const fileInput = document.getElementById('ca-file-input');
        const btnSelect = document.getElementById('ca-btn-select');
        const dropZone = document.getElementById('ca-drop-zone');
        const btnAnalyze = document.getElementById('ca-btn-analyze');
        const btnCommit = document.getElementById('ca-btn-commit');

        btnSelect.addEventListener('click', () => fileInput.click());

        fileInput.addEventListener('change', (e) => this.handleFiles(e.target.files));

        dropZone.addEventListener('dragover', (e) => {
            e.preventDefault();
            dropZone.style.backgroundColor = '#f1f5f9';
        });
        dropZone.addEventListener('dragleave', (e) => {
            e.preventDefault();
            dropZone.style.backgroundColor = 'transparent';
        });
        dropZone.addEventListener('drop', (e) => {
            e.preventDefault();
            dropZone.style.backgroundColor = 'transparent';
            this.handleFiles(e.dataTransfer.files);
        });

        btnAnalyze.addEventListener('click', () => this.startAnalysis());
        btnCommit.addEventListener('click', () => this.commitAnalysis());
    },

    async handleFiles(files) {
        if (!files || files.length === 0) return;

        const formData = new FormData();
        for (let i = 0; i < files.length; i++) {
            formData.append('files', files[i]);
        }

        try {
            toast('Dosyalar yükleniyor...', 'info');
            const res = await fetch('/api/catalog-analysis/upload', {
                method: 'POST',
                body: formData
            });
            const data = await res.json();

            if (data.success && data.files) {
                this._uploadedFiles.push(...data.files);
                this.renderQueue();
                toast('Dosyalar işlem sırasına alındı.', 'success');
            } else {
                toast('Yükleme hatası: ' + (data.error || ''), 'error');
            }
        } catch (err) {
            toast('Bağlantı hatası.', 'error');
        }
    },

    renderQueue() {
        const container = document.getElementById('ca-queue-container');
        const ul = document.getElementById('ca-queue-list');

        if (this._uploadedFiles.length === 0) {
            container.classList.add('hidden');
            return;
        }

        container.classList.remove('hidden');
        ul.innerHTML = this._uploadedFiles.map(f => `
      <li style="padding: 10px; border: 1px solid #eee; margin-bottom: 5px; border-radius: 4px; display:flex; justify-content: space-between;">
        <span>📄 ${f.split('_').pop()}</span>
        <span style="color:#f59e0b; font-size: 0.9em;">Bekliyor</span>
      </li>
    `).join('');
    },

    async startAnalysis() {
        if (this._uploadedFiles.length === 0) return;

        const brandId = parseInt(document.getElementById('ca-brand-select').value);
        if (!brandId) {
            toast('Lütfen analizi başlatmadan önce bir Marka seçin.', 'error');
            return;
        }

        // Process the first file in queue (or you could loop them sequentially)
        const fileToAnalyze = this._uploadedFiles[0];

        toast(`${fileToAnalyze.split('_').pop()} analiz ediliyor. İşlem biraz zaman alabilir...`, 'info');

        try {
            const res = await fetch('/api/catalog-analysis/analyze', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    fileName: fileToAnalyze,
                    brandId: brandId
                })
            });

            const data = await res.json();

            if (data.success) {
                toast('Analiz tamamlandı!', 'success');
                // Remove from queue
                this._uploadedFiles.shift();
                this.renderQueue();

                // Show review
                this._analysisResults = data.items;
                this.renderReviewTarget(fileToAnalyze);
            } else {
                toast('Hata: ' + data.error, 'error');
            }
        } catch (err) {
            toast('Analiz sırasında bağlantı hatası.', 'error');
        }
    },

    renderReviewTarget(filename) {
        const reviewCard = document.getElementById('ca-review-card');
        reviewCard.classList.remove('hidden');
        document.getElementById('ca-review-filename').textContent = `(${filename.split('_').pop()})`;

        const tbody = document.getElementById('ca-review-tbody');

        if (!this._analysisResults || this._analysisResults.length === 0) {
            tbody.innerHTML = `<tr><td colspan="5" class="text-center text-muted">Hiçbir ürün veya koleksiyon tespit edilemedi. (Regex yetersiz olabilir)</td></tr>`;
            return;
        }

        tbody.innerHTML = this._analysisResults.map((item, idx) => `
      <tr>
        <td>
          ${item.extractedImagePath
                ? `<img src="${item.extractedImagePath}" style="width:40px;height:40px;object-fit:contain; border-radius:4px; border:1px solid #ddd;" />`
                : `<span style="font-size:12px; color:#999;">Yok</span>`}
        </td>
        <td>
            <input type="text" class="form-control ca-input-mold" data-idx="${idx}" value="${item.moldCode || ''}" style="width:100px;"/>
        </td>
        <td>
            <input type="text" class="form-control ca-input-code" data-idx="${idx}" value="${item.productCode}" style="width:120px;"/>
        </td>
        <td>
            <input type="text" class="form-control ca-input-name" data-idx="${idx}" value="${item.productName}" />
        </td>
        <td>
            <input type="text" class="form-control ca-input-coll" data-idx="${idx}" value="${item.collectionName}" />
        </td>
        <td>
            <input type="number" class="form-control ca-input-qty" data-idx="${idx}" value="${item.unitsPerCase || ''}" style="width:80px;"/>
        </td>
        <td class="text-muted">Sayfa ${item.pageNumber}</td>
      </tr>
    `).join('');

        // Bind edits back to array
        document.querySelectorAll('.ca-input-code').forEach(el =>
            el.addEventListener('blur', e => this._analysisResults[e.target.dataset.idx].productCode = e.target.value)
        );
        document.querySelectorAll('.ca-input-mold').forEach(el =>
            el.addEventListener('blur', e => this._analysisResults[e.target.dataset.idx].moldCode = e.target.value)
        );
        document.querySelectorAll('.ca-input-name').forEach(el =>
            el.addEventListener('blur', e => this._analysisResults[e.target.dataset.idx].productName = e.target.value)
        );
        document.querySelectorAll('.ca-input-coll').forEach(el =>
            el.addEventListener('blur', e => this._analysisResults[e.target.dataset.idx].collectionName = e.target.value)
        );
        document.querySelectorAll('.ca-input-qty').forEach(el =>
            el.addEventListener('blur', e => this._analysisResults[e.target.dataset.idx].unitsPerCase = parseInt(e.target.value) || null)
        );
    },

    async commitAnalysis() {
        const catalogId = parseInt(document.getElementById('ca-catalog-select').value);

        if (!catalogId) {
            toast('Lütfen hedef kataloğu seçiniz.', 'error');
            return;
        }

        if (!this._analysisResults || this._analysisResults.length === 0) {
            toast('Kaydedilecek geçerli bir veri bulunmuyor.', 'error');
            return;
        }

        try {
            toast('Veritabanına işleniyor...', 'info');
            const res = await fetch('/api/catalog-analysis/commit', {
                method: 'POST',
                headers: { 'Content-Type': 'application/json' },
                body: JSON.stringify({
                    catalogId: catalogId,
                    items: this._analysisResults
                })
            });

            const data = await res.json();

            if (data.success) {
                toast(`Başarılı! ${data.created} ürün ve ${data.newCollections} koleksiyon eşleştirildi.`, 'success');

                // Re-render empty review
                this._analysisResults = null;
                document.getElementById('ca-review-card').classList.add('hidden');

                // If more files exist, automatically urge next processing.
                if (this._uploadedFiles.length > 0) {
                    toast('Sırada daha fazla dosya var. Sonraki dosya için analiz yapabilirsiniz.', 'info');
                }
            } else {
                toast('Kayıt hatası', 'error');
            }
        } catch {
            toast('Bağlantı hatası.', 'error');
        }
    }
};
