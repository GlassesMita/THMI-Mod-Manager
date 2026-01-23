// OOJS-UI 弹窗管理器
const OOUIModalManager = {
    windowManager: null,
    modals: [],
    dialogId: 'oo-ui-custom-dialog',
    
    init: function() {
        this.createDialogContainer();
        if (typeof OO !== 'undefined' && OO.ui) {
            console.log('OOJS-UI Modal Manager initialized');
        } else {
            console.error('OOJS-UI not loaded');
        }
    },
    
    createDialogContainer: function() {
        if (!document.getElementById(this.dialogId)) {
            const dialog = document.createElement('div');
            dialog.id = this.dialogId;
            dialog.className = 'oo-ui-dialog-overlay';
            dialog.style.cssText = 'display: none; position: fixed; top: 0; left: 0; width: 100%; height: 100%; background-color: rgba(255, 255, 255, 0.5); z-index: 1000;';
            dialog.innerHTML = `
                <div class="oo-ui-window-frame oo-ui-window-centered" style="top: 50%; left: 50%; transform: translate(-50%, -50%); width: 320px; max-width: 90%;">
                    <div class="oo-ui-window-focusTrap" tabindex="0"></div>
                    <div class="oo-ui-window-content oo-ui-dialog-content oo-ui-messageDialog-content">
                        <div class="oo-ui-window-head"></div>
                        <div class="oo-ui-window-body">
                            <div class="oo-ui-messageDialog-container">
                                <div class="oo-ui-messageDialog-text">
                                    <label class="oo-ui-messageDialog-title"></label>
                                    <label class="oo-ui-messageDialog-message"></label>
                                </div>
                            </div>
                        </div>
                        <div class="oo-ui-window-foot">
                            <div class="oo-ui-messageDialog-actions oo-ui-messageDialog-actions-horizontal">
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-safe oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-primary oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                            </div>
                        </div>
                    </div>
                    <div class="oo-ui-window-focusTrap" tabindex="0"></div>
                </div>
            `;
            document.body.appendChild(dialog);
        }
    },
    
    showConfirmStopGameDialog: function(message, onConfirm, onCancel) {
        const dialog = document.getElementById(this.dialogId);
        if (!dialog) {
            this.createDialogContainer();
            return this.showConfirmStopGameDialog(message, onConfirm, onCancel);
        }
        
        const frame = dialog.querySelector('.oo-ui-window-frame');
        const titleEl = dialog.querySelector('.oo-ui-messageDialog-title');
        const messageEl = dialog.querySelector('.oo-ui-messageDialog-message');
        const cancelBtn = dialog.querySelector('.oo-ui-flaggedElement-safe .oo-ui-buttonElement-button');
        const confirmBtn = dialog.querySelector('.oo-ui-flaggedElement-primary .oo-ui-buttonElement-button');
        const cancelLabel = cancelBtn.querySelector('.oo-ui-labelElement-label');
        const confirmLabel = confirmBtn.querySelector('.oo-ui-labelElement-label');
        
        titleEl.textContent = '关闭游戏';
        messageEl.textContent = message || '确定要停止游戏吗？';
        cancelLabel.textContent = '取消';
        confirmLabel.textContent = '确定';
        
        dialog.style.display = 'flex';
        frame.style.opacity = '1';
        frame.style.transform = 'translate(-50%, -50%)';
        frame.style.height = '20%';
        
        const handleConfirm = () => {
            cleanup();
            if (onConfirm) onConfirm();
        };
        
        const handleCancel = () => {
            cleanup();
            if (onCancel) onCancel();
        };
        
        const cleanup = () => {
            dialog.style.display = 'none';
            frame.style.height = '';
            cancelBtn.removeEventListener('click', handleCancel);
            confirmBtn.removeEventListener('click', handleConfirm);
        };
        
        cancelBtn.addEventListener('click', handleCancel);
        confirmBtn.addEventListener('click', handleConfirm);
        
        return {
            close: cleanup
        };
    },
    
    showConfirmDialog: function(title, message, onConfirm, onCancel, options = {}) {
        const dialog = document.getElementById(this.dialogId);
        if (!dialog) {
            this.createDialogContainer();
            return this.showConfirmDialog(title, message, onConfirm, onCancel, options);
        }
        
        const frame = dialog.querySelector('.oo-ui-window-frame');
        const titleEl = dialog.querySelector('.oo-ui-messageDialog-title');
        const messageEl = dialog.querySelector('.oo-ui-messageDialog-message');
        const cancelBtn = dialog.querySelector('.oo-ui-flaggedElement-safe .oo-ui-buttonElement-button');
        const confirmBtn = dialog.querySelector('.oo-ui-flaggedElement-primary .oo-ui-buttonElement-button');
        const cancelLabel = cancelBtn.querySelector('.oo-ui-labelElement-label');
        const confirmLabel = confirmBtn.querySelector('.oo-ui-labelElement-label');
        
        titleEl.textContent = title || '';
        messageEl.textContent = message || '';
        cancelLabel.textContent = options.cancelText || '取消';
        confirmLabel.textContent = options.confirmText || '确定';
        
        dialog.style.display = 'flex';
        frame.style.opacity = '1';
        frame.style.transform = 'translate(-50%, -50%)';
        
        const handleConfirm = () => {
            cleanup();
            if (onConfirm) onConfirm();
        };
        
        const handleCancel = () => {
            cleanup();
            if (onCancel) onCancel();
        };
        
        const cleanup = () => {
            dialog.style.display = 'none';
            cancelBtn.removeEventListener('click', handleCancel);
            confirmBtn.removeEventListener('click', handleConfirm);
        };
        
        cancelBtn.addEventListener('click', handleCancel);
        confirmBtn.addEventListener('click', handleConfirm);
        
        return {
            close: cleanup
        };
    },
    
    closeAllModals: function() {
        const dialog = document.getElementById(this.dialogId);
        if (dialog) {
            dialog.style.display = 'none';
        }
    }
};

document.addEventListener('DOMContentLoaded', function() {
    OOUIModalManager.init();
});

window.OOUIModalManager = OOUIModalManager;
