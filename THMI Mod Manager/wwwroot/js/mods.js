// 动态设置主题色变量
function initThemeColor() {
    const themeColorElement = document.getElementById('themeColorHidden');
    if (themeColorElement) {
        const themeColor = themeColorElement.value;
        document.documentElement.style.setProperty('--theme-color', themeColor);
    }
}

// 初始化Mods页面功能
function initModsPage() {
    initThemeColor();
    
    // 获取安装按钮
    const installButton = document.getElementById('installModButton');
    
    // 添加点击事件处理
    if (installButton) {
        installButton.addEventListener('click', function() {
            console.log('Install button clicked, checking for file browser...');
            console.log('window.fileBrowser:', window.fileBrowser);
            console.log('typeof window.fileBrowser:', typeof window.fileBrowser);
            
            // 确保window.fileBrowser对象存在
            if (typeof window.fileBrowser !== 'undefined' && window.fileBrowser !== null) {
                console.log('File browser is available, configuring...');
                // 配置文件浏览器
                window.fileBrowser.setAllowedExtensions(['.zip', '.izakaya']);
                window.fileBrowser.onFileSelected = function(filePath) {
                    console.log('Selected mod file:', filePath);
                    
                    // 这里可以添加实际的Mod安装逻辑
                    alert('Selected file: ' + filePath);
                    
                    // 重置回调，避免内存泄漏
                    window.fileBrowser.onFileSelected = null;
                };
                
                // 打开文件浏览器，设置标题和过滤器
                window.fileBrowser.open('file', {
                    title: 'Select Mod File',
                    extensions: ['.zip', '.izakaya']
                });
                console.log('File browser opened');
            } else {
                console.error('File browser is not available');
                alert('File browser is not available. Please refresh the page and try again.');
            }
        });
    }
}

// 页面加载完成后初始化
if (document.readyState === 'loading') {
    document.addEventListener('DOMContentLoaded', initModsPage);
} else {
    initModsPage();
}