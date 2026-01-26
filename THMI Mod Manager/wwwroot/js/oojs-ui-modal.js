// OOJS-UI 弹窗管理器
const OOUIModalManager = {
    windowManager: null,
    modals: [],
    
    init: function() {
        this.createExitDialog();
        this.createStopGameDialog();
        if (typeof OO !== 'undefined' && OO.ui) {
            console.log('OOJS-UI Modal Manager initialized');
        } else {
            console.error('OOJS-UI not loaded');
        }
    },
    
    createExitDialog: function() {
        if (!document.getElementById('oo-ui-exit-dialog')) {
            const dialog = document.createElement('div');
            dialog.id = 'oo-ui-exit-dialog';
            dialog.className = 'oo-ui-dialog-overlay';
            dialog.style.cssText = 'display: none; position: fixed; top: 0px; left: 0px; width: 100%; height: 100%; background-color: rgba(255,255,255,0.5); z-index: 1000;';
            dialog.innerHTML = `
                <div class="oo-ui-window-frame oo-ui-window-centered" style="position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); width: 320px; max-width: 90%; min-height: auto; background-color: white; border-radius: 4px; box-shadow: rgba(0, 0, 0, 0.15) 0px 4px 12px; height: 15%;">
                    <div class="oo-ui-window-content oo-ui-dialog-content oo-ui-messageDialog-content" style="padding: 20px; display: flex; flex-direction: column;">
                        <div class="oo-ui-window-body" style="flex: 1; display: flex; align-items: center; justify-content: center;">
                            <div class="oo-ui-messageDialog-container" style="width: 100%; text-align: center;">
                                <div class="oo-ui-messageDialog-text">
                                    <label class="oo-ui-messageDialog-title" style="display: block; font-weight: bold; margin-bottom: 12px; font-size: 16px; display: none;"></label>
                                    <label class="oo-ui-messageDialog-message" style="display: block; font-size: 14px; line-height: 1.5;"></label>
                                </div>
                            </div>
                        </div>
                        <div class="oo-ui-window-foot" style="margin-top: 20px;">
                            <div class="oo-ui-messageDialog-actions oo-ui-messageDialog-actions-horizontal" style="display: flex; justify-content: center; gap: 8px;">
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-safe oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0" style="display: inline-block; padding: 8px 16px;">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-primary oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0" style="display: inline-block; padding: 8px 16px;">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(dialog);
        }
    },
    
    createStopGameDialog: function() {
        if (!document.getElementById('oo-ui-stop-game-dialog')) {
            const dialog = document.createElement('div');
            dialog.id = 'oo-ui-stop-game-dialog';
            dialog.className = 'oo-ui-dialog-overlay';
            dialog.style.cssText = 'display: none; position: fixed; top: 0px; left: 0px; width: 100%; height: 100%; background-color: rgba(255,255,255,0.5); z-index: 1000;';
            dialog.innerHTML = `
                <div class="oo-ui-window-frame oo-ui-window-centered" style="position: fixed; top: 50%; left: 50%; transform: translate(-50%, -50%); width: 320px; max-width: 90%; min-height: auto; background-color: white; border-radius: 4px; box-shadow: rgba(0, 0, 0, 0.15) 0px 4px 12px; height: 15%;">
                    <div class="oo-ui-window-content oo-ui-dialog-content oo-ui-messageDialog-content" style="padding: 20px; display: flex; flex-direction: column;">
                        <div class="oo-ui-window-body" style="flex: 1; display: flex; align-items: center; justify-content: center;">
                            <div class="oo-ui-messageDialog-container" style="width: 100%; text-align: center;">
                                <div class="oo-ui-messageDialog-text">
                                    <label class="oo-ui-messageDialog-title" style="display: block; font-weight: bold; margin-bottom: 12px; font-size: 16px; display: none;"></label>
                                    <label class="oo-ui-messageDialog-message" style="display: block; font-size: 14px; line-height: 1.5;"></label>
                                </div>
                            </div>
                        </div>
                        <div class="oo-ui-window-foot" style="margin-top: 20px;">
                            <div class="oo-ui-messageDialog-actions oo-ui-messageDialog-actions-horizontal" style="display: flex; justify-content: center; gap: 8px;">
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-safe oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0" style="display: inline-block; padding: 8px 16px;">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                                <span class="oo-ui-buttonElement oo-ui-labelElement oo-ui-flaggedElement-primary oo-ui-buttonWidget oo-ui-buttonElement-framed">
                                    <a class="oo-ui-buttonElement-button" role="button" tabindex="0" style="display: inline-block; padding: 8px 16px;">
                                        <span class="oo-ui-labelElement-label"></span>
                                    </a>
                                </span>
                            </div>
                        </div>
                    </div>
                </div>
            `;
            document.body.appendChild(dialog);
        }
    },
    
    showExitDialog: function(title, message, onConfirm, onCancel, options = {}) {
        const dialog = document.getElementById('oo-ui-exit-dialog');
        if (!dialog) {
            this.createExitDialog();
            return this.showExitDialog(title, message, onConfirm, onCancel, options);
        }
        
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
        
        const handleConfirm = () => {
            cleanup();
            if (onConfirm) onConfirm();
        };
        
        const handleCancel = () => {
            cleanup();
            if (onCancel) onCancel();
        };
        
        const handleOverlayClick = (e) => {
            if (e.target === dialog) {
                cleanup();
                if (onCancel) onCancel();
            }
        };
        
        const handleEscKey = (e) => {
            if (e.key === 'Escape') {
                cleanup();
                if (onCancel) onCancel();
            }
        };
        
        const cleanup = () => {
            dialog.style.display = 'none';
            cancelBtn.removeEventListener('click', handleCancel);
            confirmBtn.removeEventListener('click', handleConfirm);
            dialog.removeEventListener('click', handleOverlayClick);
            document.removeEventListener('keydown', handleEscKey);
        };
        
        cancelBtn.addEventListener('click', handleCancel);
        confirmBtn.addEventListener('click', handleConfirm);
        dialog.addEventListener('click', handleOverlayClick);
        document.addEventListener('keydown', handleEscKey);
        
        return {
            close: cleanup
        };
    },
    
    showStopGameDialog: function(title, message, onConfirm, onCancel, options = {}) {
        const dialog = document.getElementById('oo-ui-stop-game-dialog');
        if (!dialog) {
            this.createStopGameDialog();
            return this.showStopGameDialog(title, message, onConfirm, onCancel, options);
        }
        
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
        
        const handleConfirm = () => {
            cleanup();
            if (onConfirm) onConfirm();
        };
        
        const handleCancel = () => {
            cleanup();
            if (onCancel) onCancel();
        };
        
        const handleOverlayClick = (e) => {
            if (e.target === dialog) {
                cleanup();
                if (onCancel) onCancel();
            }
        };
        
        const handleEscKey = (e) => {
            if (e.key === 'Escape') {
                cleanup();
                if (onCancel) onCancel();
            }
        };
        
        const cleanup = () => {
            dialog.style.display = 'none';
            cancelBtn.removeEventListener('click', handleCancel);
            confirmBtn.removeEventListener('click', handleConfirm);
            dialog.removeEventListener('click', handleOverlayClick);
            document.removeEventListener('keydown', handleEscKey);
        };
        
        cancelBtn.addEventListener('click', handleCancel);
        confirmBtn.addEventListener('click', handleConfirm);
        dialog.addEventListener('click', handleOverlayClick);
        document.addEventListener('keydown', handleEscKey);
        
        return {
            close: cleanup
        };
    },
    
    closeAllModals: function() {
        const exitDialog = document.getElementById('oo-ui-exit-dialog');
        const stopGameDialog = document.getElementById('oo-ui-stop-game-dialog');
        if (exitDialog) {
            exitDialog.style.display = 'none';
        }
        if (stopGameDialog) {
            stopGameDialog.style.display = 'none';
        }
    }
};

document.addEventListener('DOMContentLoaded', function() {
    OOUIModalManager.init();
});

window.OOUIModalManager = OOUIModalManager;
