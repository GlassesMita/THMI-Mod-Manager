const modsManager = {
    apiEndpoint: window.location.origin + '/api/mods',
    modsList: [],
    sortOrder: 'name', // 'name' or 'date'
    
    init: function() {
        this.loadLocalizedStrings().then(() => {
            this.loadMods();
            this.bindEvents();
        });
    },
    
    bindEvents: function() {
        const refreshButton = document.getElementById('refreshModsButton');
        if (refreshButton) {
            refreshButton.addEventListener('click', () => {
                this.loadMods();
            });
        }
    },
    
    changeSortOrder: function(order) {
        this.sortOrder = order;
        this.loadMods();
    },
    
    loadMods: async function() {
        const modsListElement = document.getElementById('modsList');
        const modsEmptyElement = document.getElementById('modsEmpty');
        const modsErrorElement = document.getElementById('modsError');
        
        if (!modsListElement) return;
        
        modsListElement.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border" role="status">
                    <span class="visually-hidden">加载中...</span>
                </div>
            </div>
        `;
        
        modsEmptyElement.style.display = 'none';
        modsErrorElement.style.display = 'none';
        
        try {
            const response = await fetch(this.apiEndpoint);
            console.log('API Request URL:', this.apiEndpoint);
            console.log('Response status:', response.status);
            console.log('Response type:', response.headers.get('content-type'));
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const data = await response.json();
            
            if (data.success) {
                this.modsList = data.mods || [];
                this.renderMods();
            } else {
                throw new Error(data.message || '加载失败');
            }
        } catch (error) {
            console.error('加载Mod失败:', error);
            modsListElement.innerHTML = '';
            modsErrorElement.style.display = 'block';
            
            const errorMessageElement = document.getElementById('modsErrorMessage');
            if (errorMessageElement) {
                errorMessageElement.textContent = `加载Mod时发生错误: ${error.message}`;
            }
        }
    },
    
    renderMods: function() {
        const modsListElement = document.getElementById('modsList');
        const modsEmptyElement = document.getElementById('modsEmpty');
        
        if (!modsListElement) return;
        
        if (this.modsList.length === 0) {
            modsListElement.innerHTML = '';
            modsEmptyElement.style.display = 'block';
            return;
        }
        
        modsEmptyElement.style.display = 'none';
        
        let html = '<div class="mod-list">';
        
        this.modsList.forEach(mod => {
            html += this.renderModItem(mod);
        });
        
        html += '</div>';
        modsListElement.innerHTML = html;
    },
    
    renderModItem: function(mod) {
        const isValid = mod.isValid;
        const statusClass = isValid ? 'text-success' : 'text-warning';
        const statusIcon = isValid ? 'bi-check-circle' : 'bi-exclamation-circle';
        const statusText = isValid ? (this.localizedStrings.loadedStatus || '已加载') : (this.localizedStrings.loadFailedStatus || '加载失败');
        const isDisabled = mod.isDisabled; // Use the new IsDisabled property
        const buttonClass = isDisabled ? 'btn-success' : 'btn-warning';
        const buttonText = isDisabled ? this.localizedStrings.enableButton || '启用' : this.localizedStrings.disableButton || '禁用';
        const buttonIcon = isDisabled ? 'bi-play-fill' : 'bi-pause-fill';
        
        // Generate a unique ID for this mod item
        const modId = this.generateModId(mod.fileName);
        
        return `
            <div class="mod-item ${isValid ? '' : 'border-warning'} ${isDisabled ? 'disabled' : ''}">
                <div class="mod-item-main">
                    <div class="mod-item-header">
                        <div class="mod-item-title">
                            <div class="mod-title-container">
                                <h5 class="mb-0">${this.escapeHtml(mod.name || mod.fileName)}</h5>
                            </div>
                        </div>
                        <div class="mod-item-actions">
                            <button class="btn btn-outline-secondary btn-sm me-2" onclick="toggleModDetails('${modId}')">
                                <i class="bi bi-chevron-down" id="toggle-icon-${modId}"></i>
                            </button>
                            <button class="btn ${buttonClass} btn-sm me-2 mod-action-btn" onclick="modsManager.toggleMod('${this.escapeHtml(mod.fileName)}')">
                                <i class="bi ${buttonIcon}"></i>
                                ${buttonText}
                            </button>
                            <button class="btn btn-danger btn-sm mod-action-btn" onclick="confirmDeleteMod('${this.escapeHtml(mod.fileName)}')">
                                <i class="bi bi-trash-fill"></i>
                                ${this.localizedStrings.deleteButton || '删除'}
                            </button>
                        </div>
                    </div>
                    <div class="mod-item-body" id="mod-details-${modId}" style="display: none;">
                        ${mod.modLink ? `<span class="mod-item-info"><strong>Link:</strong> <a href="${this.escapeHtml(mod.modLink)}" target="_blank" rel="noopener noreferrer">${this.escapeHtml(mod.modLink)}</a></span><br />` : ''}
                        ${mod.author ? `<span class="mod-item-info"><strong>${this.localizedStrings.authorLabel || '作者'}:</strong> ${this.escapeHtml(mod.author)}</span>` : ''}
                        <br />
                        ${mod.uniqueId ? `<span class="mod-item-info"><strong>ID:</strong> <code>${this.escapeHtml(mod.uniqueId)}</code></span>` : ''}
                        ${mod.description ? `<p class="mod-item-description text-muted small mb-0"><strong>${this.localizedStrings.descriptionLabel || 'Mod Desc'}:</strong><br /> ${this.escapeHtml(mod.description)}</p>` : ''}
                    </div>
                    <div class="mod-item-footer">
                        <span class="small text-muted">
                            <i class="bi bi-file-earmark-code"></i>
                            ${this.escapeHtml(mod.fileName)}
                        </span>
                        <div class="mod-footer-right">
                            ${mod.installTime ? `<span class="mod-install-time"><i class="bi bi-clock-history"></i> ${this.formatInstallTime(mod.installTime)}</span>` : ''}
                            ${mod.version ? `<span class="mod-version-footer">${this.escapeHtml(mod.version)}${mod.versionCode ? ` (${mod.versionCode})` : ''}</span>` : ''}
                            ${!isValid && mod.errorMessage ? `
                                <span class="text-warning small">
                                    <i class="bi bi-exclamation-triangle-fill"></i>
                                    ${this.escapeHtml(mod.errorMessage)}
                                </span>
                            ` : ''}
                        </div>
                    </div>
                </div>
            </div>
        `;
    },
    
    generateModId: function(fileName) {
        // Generate a unique ID based on the filename
        return fileName.replace(/[^a-zA-Z0-9]/g, '_');
    },
    
    toggleMod: async function(fileName) {
        try {
            const response = await fetch(`${this.apiEndpoint}/toggle`, {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/json',
                },
                body: JSON.stringify({ fileName: fileName })
            });
            
            if (!response.ok) {
                const errorText = await response.text();
                console.error('Server error:', errorText);
                throw new Error(`HTTP ${response.status}: ${errorText}`);
            }
            
            const data = await response.json();
            
            if (data.success) {
                await this.loadMods();
            } else {
                alert('操作失败: ' + data.message);
            }
        } catch (error) {
            console.error('切换Mod状态时出错:', error);
            alert('切换Mod状态时出错: ' + error.message);
        }
    },
    
    formatFileSize: function(bytes) {
        if (bytes === 0) return '0 B';
        const k = 1024;
        const sizes = ['B', 'KB', 'MB', 'GB'];
        const i = Math.floor(Math.log(bytes) / Math.log(k));
        return parseFloat((bytes / Math.pow(k, i)).toFixed(2)) + ' ' + sizes[i];
    },
    
    formatDate: function(dateString) {
        try {
            const date = new Date(dateString);
            return date.toLocaleString();
        } catch (error) {
            return dateString;
        }
    },
    
    formatInstallTime: function(dateString) {
        try {
            const date = new Date(dateString);
            const year = date.getFullYear();
            const month = String(date.getMonth() + 1).padStart(2, '0');
            const day = String(date.getDate()).padStart(2, '0');
            const hours = String(date.getHours()).padStart(2, '0');
            const minutes = String(date.getMinutes()).padStart(2, '0');
            const seconds = String(date.getSeconds()).padStart(2, '0');
            return `${year}/${month}/${day} ${hours}:${minutes}:${seconds}`;
        } catch (error) {
            return dateString;
        }
    },
    
    escapeHtml: function(text) {
        if (!text) return '';
        const div = document.createElement('div');
        div.textContent = text;
        return div.innerHTML;
    },
    
    loadLocalizedStrings: async function() {
        try {
            const response = await fetch(window.location.origin + '/api/mods/localized-strings');
            const data = await response.json();
            this.localizedStrings = data;
        } catch (error) {
            console.error('Error loading localized strings:', error);
            // Set default values
            this.localizedStrings = {
                gameRunningWarning: '游戏正在运行，无法修改Mod状态',
                gameRunningTooltip: '游戏运行时无法执行此操作',
                enableButton: '启用',
                disableButton: '禁用',
                deleteButton: '删除'
            };
        }
    },
    
    checkGameStatus: async function() {
        try {
            const response = await fetch(window.location.origin + '/api/mods/game-status');
            const data = await response.json();
            const gameRunning = data.isRunning;
            
            // Update all mod action buttons based on game status
            const actionButtons = document.querySelectorAll('.mod-action-btn');
            actionButtons.forEach(button => {
                button.disabled = gameRunning;
                if (gameRunning) {
                    button.title = this.localizedStrings.gameRunningTooltip || '游戏运行时无法执行此操作';
                } else {
                    button.title = '';
                }
            });
            
            // Show/hide game running warning
            const gameStatusWarning = document.getElementById('gameStatusWarning');
            if (gameRunning && !gameStatusWarning) {
                const warningElement = document.createElement('div');
                warningElement.id = 'gameStatusWarning';
                warningElement.className = 'alert alert-warning mt-3';
                warningElement.innerHTML = '<i class="bi bi-exclamation-triangle-fill"></i> ' + (this.localizedStrings.gameRunningWarning || '游戏正在运行，无法修改Mod状态');
                document.querySelector('.mods-content').prepend(warningElement);
            } else if (!gameRunning && gameStatusWarning) {
                gameStatusWarning.remove();
            }
            
            return gameRunning;
        } catch (error) {
            console.error('Error checking game status:', error);
            return false;
        }
    },
    
    loadMods: async function() {
        // Check game status before loading mods
        const gameRunning = await this.checkGameStatus();
        
        const modsListElement = document.getElementById('modsList');
        const modsEmptyElement = document.getElementById('modsEmpty');
        const modsErrorElement = document.getElementById('modsError');
        
        if (!modsListElement) return;
        
        modsListElement.innerHTML = `
            <div class="text-center py-5">
                <div class="spinner-border" role="status">
                    <span class="visually-hidden">加载中...</span>
                </div>
            </div>
        `;
        
        modsEmptyElement.style.display = 'none';
        modsErrorElement.style.display = 'none';
        
        try {
            const response = await fetch(this.apiEndpoint);
            
            if (!response.ok) {
                throw new Error(`HTTP ${response.status}`);
            }
            
            const data = await response.json();
            
            if (data.success) {
                this.modsList = data.mods || [];
                this.renderMods();
                
                // If game is running, disable all action buttons
                if (gameRunning) {
                    const actionButtons = document.querySelectorAll('.mod-action-btn');
                    actionButtons.forEach(button => {
                        button.disabled = true;
                        button.title = '游戏运行时无法执行此操作';
                    });
                }
            } else {
                throw new Error(data.message || '加载失败');
            }
        } catch (error) {
            console.error('加载Mod失败:', error);
            modsListElement.innerHTML = '';
            modsErrorElement.style.display = 'block';
            
            const errorMessageElement = document.getElementById('modsErrorMessage');
            if (errorMessageElement) {
                errorMessageElement.textContent = `加载Mod时发生错误: ${error.message}`;
            }
        }
    }
};

// 定期检查游戏状态
setInterval(() => {
    if (typeof modsManager !== 'undefined') {
        modsManager.checkGameStatus();
    }
}, 3000);

document.addEventListener('DOMContentLoaded', function() {
    modsManager.init();
});
