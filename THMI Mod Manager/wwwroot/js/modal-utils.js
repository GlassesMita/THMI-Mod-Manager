// 弹窗工具类，用于替换原生 alert 和 confirm
const ModalUtils = {
    // 显示确认对话框（退出应用）
    confirmExit: function(title, message, onConfirm, onCancel, options = {}) {
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showExitDialog(title, message, onConfirm, onCancel, options);
        }
        
        if (confirm(message)) {
            if (onConfirm) onConfirm();
            return true;
        }
        if (onCancel) onCancel();
        return false;
    },
    
    // 显示确认对话框（关闭游戏）
    confirmStopGame: function(title, message, onConfirm, onCancel, options = {}) {
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showStopGameDialog(title, message, onConfirm, onCancel, options);
        }
        
        if (confirm(message)) {
            if (onConfirm) onConfirm();
            return true;
        }
        if (onCancel) onCancel();
        return false;
    },
    
    // 显示信息对话框
    alert: function(title, message, onClose, options = {}) {
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showExitDialog(title, message, onClose, null, options);
        }
        
        window.alert(message);
        if (onClose) onClose();
    },
    
    // 显示警告对话框
    warning: function(title, message, onConfirm, onCancel, options = {}) {
        if (typeof OOUIModalManager !== 'undefined') {
            return OOUIModalManager.showExitDialog(title, message, onConfirm, onCancel, options);
        }
        
        if (confirm(message)) {
            if (onConfirm) onConfirm();
            return true;
        }
        if (onCancel) onCancel();
        return false;
    },
    
    // 显示加载对话框
    loading: function(title, message) {
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
