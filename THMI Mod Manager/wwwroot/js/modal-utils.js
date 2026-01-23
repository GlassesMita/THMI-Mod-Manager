// 弹窗工具类，用于替换原生 alert 和 confirm
const ModalUtils = {
    // 显示确认对话框
    confirm: function(title, message, onConfirm, onCancel, options = {}) {
        // 如果 OOJS-UI 可用，使用 OOJS-UI
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showConfirmDialog(title, message, onConfirm, onCancel, options);
        }
        
        // 回退到原生 confirm
        if (confirm(message)) {
            if (onConfirm) onConfirm();
            return true;
        }
        if (onCancel) onCancel();
        return false;
    },
    
    // 显示信息对话框
    alert: function(title, message, onClose, options = {}) {
        // 如果 OOJS-UI 可用，使用 OOJS-UI
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showInfoDialog(title, message, onClose, options);
        }
        
        // 回退到原生 alert
        window.alert(message);
        if (onClose) onClose();
    },
    
    // 显示警告对话框
    warning: function(title, message, onConfirm, onCancel, options = {}) {
        // 如果 OOJS-UI 可用，使用 OOJS-UI
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showWarningDialog(title, message, onConfirm, onCancel, options);
        }
        
        // 回退到原生 confirm
        if (confirm(message)) {
            if (onConfirm) onConfirm();
            return true;
        }
        if (onCancel) onCancel();
        return false;
    },
    
    // 显示加载对话框
    loading: function(title, message) {
        // 如果 OOJS-UI 可用，使用 OOJS-UI
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showLoadingDialog(title, message);
        }
        
        // 回退到简单的控制台输出
        console.log('Loading:', message);
        return {
            close: function() {
                console.log('Loading dialog closed');
            }
        };
    }
};

// 导出到全局
window.ModalUtils = ModalUtils;