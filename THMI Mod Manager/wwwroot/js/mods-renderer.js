(function() {
    'use strict';
    
    const ModRenderer = {
        apiEndpoint: null,
        modsList: [],
        debugMode: true,
        
        init: function() {
            this.apiEndpoint = window.location.origin + '/api/mods';
            console.log('[ModRenderer] Initializing with endpoint:', this.apiEndpoint);
            this.setupObserver();
            
            // Delay mod loading to allow page to render first
            // This improves perceived performance and reduces conflicts with browser extensions
            const doLoadMods = () => {
                this.loadMods();
            };
            
            if (window.requestIdleCallback) {
                requestIdleCallback(doLoadMods, { timeout: 3000 });
            } else {
                setTimeout(doLoadMods, 500);
            }
        },
        
        setupObserver: function() {
            const observer = new MutationObserver((mutations) => {
                mutations.forEach((mutation) => {
                    mutation.addedNodes.forEach((node) => {
                        if (node.nodeType === 1 && node.id === 'modsList') {
                            console.log('[ModRenderer] DOM mutation detected for modsList');
                            this.loadMods();
                        }
                    });
                });
            });
            
            const container = document.querySelector('.mods-content');
            if (container) {
                observer.observe(container, { childList: true, subtree: true });
            } else {
                console.warn('[ModRenderer] .mods-content container not found');
            }
        },
        
        loadMods: async function() {
            const modsListElement = document.getElementById('modsList');
            
            console.log('[ModRenderer] loadMods called, element found:', !!modsListElement);
            
            if (!modsListElement) {
                console.warn('[ModRenderer] modsList element not found, retrying in 100ms');
                setTimeout(() => this.loadMods(), 100);
                return;
            }
            
            modsListElement.innerHTML = '<div class="text-center py-5"><div class="spinner-border" role="status"><span class="visually-hidden">加载中...</span></div></div>';
            
            try {
                console.log('[ModRenderer] Fetching from:', this.apiEndpoint);
                
                const response = await fetch(this.apiEndpoint);
                console.log('[ModRenderer] Response status:', response.status);
                
                if (!response.ok) throw new Error('HTTP ' + response.status);
                
                const text = await response.text();
                console.log('[ModRenderer] Raw response length:', text.length);
                console.log('[ModRenderer] Raw response start:', text.substring(0, 200));
                
                const data = JSON.parse(text);
                console.log('[ModRenderer] Parsed JSON:', data);
                console.log('[ModRenderer] data.success:', data.success);
                console.log('[ModRenderer] data.mods:', data.mods);
                console.log('[ModRenderer] Array.isArray(data.mods):', Array.isArray(data.mods));
                
                if (data.success && Array.isArray(data.mods)) {
                    console.log('[ModRenderer] Processing', data.mods.length, 'mods...');
                    
                    this.modsList = data.mods.map((mod, index) => {
                        console.log('[ModRenderer] Raw mod', index, ':', JSON.stringify(mod).substring(0, 200));
                        return this.normalizeModData(mod, index);
                    });
                    
                    console.log('[ModRenderer] Normalized mods:', this.modsList);
                    this.render();
                } else {
                    throw new Error(data.message || 'Invalid response format');
                }
            } catch (error) {
                console.error('[ModRenderer] Error:', error);
                if (modsListElement) {
                    modsListElement.innerHTML = '<div class="alert alert-danger">加载失败: ' + error.message + '</div>';
                }
            }
        },
        
        normalizeModData: function(mod, index) {
            console.log('[ModRenderer] Normalizing mod', index, ':', mod);
            
            const getValue = (camelCase, pascalCase) => {
                const value = mod[camelCase] || mod[pascalCase];
                console.log('[ModRenderer] getValue(', camelCase, ',', pascalCase, ') =', value);
                return value || '';
            };
            
            const fileName = getValue('fileName', 'FileName');
            const name = getValue('name', 'Name');
            
            console.log('[ModRenderer] Extracted fileName:', fileName, 'name:', name);
            
            const result = {
                fileName: fileName,
                name: name || fileName.replace(/\.dll(\.disabled)?$/i, ''),
                version: getValue('version', 'Version'),
                author: getValue('author', 'Author'),
                description: getValue('description', 'Description'),
                uniqueId: getValue('uniqueId', 'UniqueId'),
                modLink: getValue('modLink', 'ModLink') || getValue('link', 'Link'),
                isValid: mod.isValid === true || mod.IsValid === true,
                isDisabled: mod.isDisabled === true || mod.IsDisabled === true || fileName.endsWith('.disabled'),
                versionCode: mod.versionCode || mod.VersionCode || 0,
                errorMessage: mod.errorMessage || mod.ErrorMessage || ''
            };
            
            console.log('[ModRenderer] Normalized result:', result);
            return result;
        },
        
        render: function() {
            const modsListElement = document.getElementById('modsList');
            if (!modsListElement) {
                console.warn('[ModRenderer] Cannot render, modsList element not found');
                return;
            }
            
            console.log('[ModRenderer] Rendering', this.modsList.length, 'mods');
            
            if (this.modsList.length === 0) {
                modsListElement.innerHTML = '<div class="mods-empty"><div class="empty-icon"><i data-icon="icon-mods"></i></div><h2>暂无 Mod</h2><p>BepInEx/plugins 目录中没有找到任何 Mod 文件。</p></div>';
                return;
            }
            
            let html = '<div class="mod-list">';
            this.modsList.forEach((mod, index) => {
                console.log('[ModRenderer] Creating HTML for mod', index, ':', mod.name);
                html += this.createModItemHtml(mod, index);
            });
            html += '</div>';
            
            console.log('[ModRenderer] Final HTML length:', html.length);
            modsListElement.innerHTML = html;
            console.log('[ModRenderer] HTML inserted, checking content:', modsListElement.innerHTML.substring(0, 200));
        },
        
        createModItemHtml: function(mod, index) {
            const fileName = mod.fileName || 'unknown-' + index;
            const name = mod.name || fileName.replace(/\.dll(\.disabled)?$/i, '');
            const isValid = mod.isValid === true;
            const isDisabled = mod.isDisabled === true;
            const buttonClass = isDisabled ? 'btn-success' : 'btn-warning';
            const buttonText = isDisabled ? '启用' : '禁用';
            const buttonIcon = isDisabled ? 'icon-play' : 'icon-pause';
            const borderClass = isValid ? '' : 'border-warning';
            
            const modId = 'mod-' + fileName.replace(/[^a-zA-Z0-9]/g, '_').replace(/\.dll$/i, '') + '-' + index;
            
            let html = '<div class="mod-item ' + borderClass + ' ' + (isDisabled ? 'disabled' : '') + '">';
            html += '<div class="mod-item-main">';
            html += '<div class="mod-item-header">';
            html += '<div class="mod-item-title"><div class="mod-title-container"><h5 class="mb-0">' + this.escapeHtml(name) + '</h5></div></div>';
            html += '<div class="mod-item-actions">';
            html += '<button class="btn btn-outline-secondary btn-sm me-2" onclick="ModRenderer.toggleDetails(\'' + modId + '\')"><i data-icon="icon-chevron-down" id="toggle-icon-' + modId + '"></i></button>';
            html += '<button class="btn ' + buttonClass + ' btn-sm me-2 mod-action-btn" onclick="ModRenderer.toggleMod(\'' + this.escapeJs(fileName) + '\')"><i data-icon="' + buttonIcon + '"></i> ' + buttonText + '</button>';
            html += '<button class="btn btn-danger btn-sm mod-action-btn" onclick="ModRenderer.confirmDelete(\'' + this.escapeJs(fileName) + '\')"><i data-icon="icon-trash"></i> 删除</button>';
            html += '</div></div>';
            html += '<div class="mod-item-body" id="mod-details-' + modId + '" style="display: none;">';
            
            if (mod.modLink) {
                html += '<span class="mod-item-info"><strong>Link:</strong> <a href="' + this.escapeHtml(mod.modLink) + '" target="_blank" rel="noopener noreferrer">' + this.escapeHtml(mod.modLink) + '</a></span><br>';
            }
            if (mod.author) {
                html += '<span class="mod-item-info"><strong>作者:</strong> ' + this.escapeHtml(mod.author) + '</span><br>';
            }
            if (mod.uniqueId) {
                html += '<span class="mod-item-info"><strong>ID:</strong> <code>' + this.escapeHtml(mod.uniqueId) + '</code></span>';
            }
            if (mod.description) {
                html += '<p class="mod-item-description text-muted small mb-0"><strong>Mod Desc:</strong><br> ' + this.escapeHtml(mod.description) + '</p>';
            }
            
            html += '</div>';
            html += '<div class="mod-item-footer">';
            html += '<span class="small text-muted"><i data-icon="icon-file-code"></i> ' + this.escapeHtml(fileName) + '</span>';
            html += '<div class="mod-footer-right">';
            
            if (mod.version) {
                html += '<span class="mod-version-footer">' + this.escapeHtml(mod.version);
                if (mod.versionCode) html += ' (' + mod.versionCode + ')';
                html += '</span>';
            }
            
            if (!isValid && mod.errorMessage) {
                html += '<span class="text-warning small"><i data-icon="icon-warning"></i> ' + this.escapeHtml(mod.errorMessage) + '</span>';
            }
            
            html += '</div></div></div></div>';
            
            return html;
        },
        
        toggleDetails: function(modId) {
            const details = document.getElementById('mod-details-' + modId);
            const icon = document.getElementById('toggle-icon-' + modId);
            
            if (details) {
                const isHidden = details.style.display === 'none' || details.style.display === '';
                details.style.display = isHidden ? 'block' : 'none';
                if (icon) {
                    icon.classList.remove(isHidden ? 'bi-chevron-down' : 'bi-chevron-up');
                    icon.classList.add(isHidden ? 'bi-chevron-up' : 'bi-chevron-down');
                }
            }
        },
        
        toggleMod: async function(fileName) {
            if (!fileName) {
                alert('无法识别Mod文件');
                return;
            }
            
            try {
                const response = await fetch(this.apiEndpoint + '/toggle', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ fileName: fileName })
                });
                
                if (!response.ok) throw new Error('HTTP ' + response.status);
                
                const data = await response.json();
                if (data.success) {
                    this.loadMods();
                } else {
                    alert('操作失败: ' + data.message);
                }
            } catch (error) {
                console.error('[ModRenderer] Toggle error:', error);
                alert('切换失败: ' + error.message);
            }
        },
        
        confirmDelete: function(fileName) {
            if (!fileName) return;
            
            const name = fileName.replace(/\.dll(\.disabled)?$/i, '');
            if (confirm('确定要删除 Mod "' + name + '" 吗？此操作不可恢复。')) {
                this.deleteMod(fileName);
            }
        },
        
        deleteMod: async function(fileName) {
            try {
                const response = await fetch('/api/mods/delete', {
                    method: 'POST',
                    headers: { 'Content-Type': 'application/json' },
                    body: JSON.stringify({ fileName: fileName })
                });
                
                const data = await response.json();
                if (data.success) {
                    this.loadMods();
                } else {
                    alert('删除失败: ' + data.message);
                }
            } catch (error) {
                console.error('[ModRenderer] Delete error:', error);
                alert('删除失败: ' + error.message);
            }
        },
        
        escapeHtml: function(text) {
            if (text === null || text === undefined) return '';
            const div = document.createElement('div');
            div.textContent = String(text);
            return div.innerHTML;
        },
        
        escapeJs: function(text) {
            if (text === null || text === undefined) return '';
            return String(text)
                .replace(/\\/g, '\\\\')
                .replace(/'/g, "\\'")
                .replace(/"/g, '\\"')
                .replace(/\n/g, '\\n')
                .replace(/\r/g, '\\r');
        },
        
        refresh: function() {
            this.loadMods();
        }
    };
    
    window.ModRenderer = ModRenderer;
    
    document.addEventListener('DOMContentLoaded', function() {
        ModRenderer.init();
    });
})();
