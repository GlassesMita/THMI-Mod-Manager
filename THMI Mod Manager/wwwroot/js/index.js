// Index页面JavaScript

// 动态设置主题色变量
function initThemeColor() {
    const themeColorElement = document.getElementById('themeColorHidden');
    if (themeColorElement) {
        const themeColor = themeColorElement.value;
        document.documentElement.style.setProperty('--theme-color', themeColor);
    }
}

// 页面加载完成后添加动画效果
document.addEventListener('DOMContentLoaded', function () {
    // 初始化主题色
    initThemeColor();
    // 为欢迎标题添加动画类
    const welcomeTitle = document.querySelector('.welcome-title');
    if (welcomeTitle) {
        welcomeTitle.classList.add('welcome-animation');
    }

    // 为警告框添加脉冲效果
    const alertBoxes = document.querySelectorAll('.animated-alert');
    alertBoxes.forEach(function (alertBox) {
        // 检查是否为CVE警告
        if (alertBox.querySelector('.alert-heading') && 
            (alertBox.querySelector('.alert-heading').textContent.includes('安全警告') || 
             alertBox.querySelector('.alert-heading').textContent.includes('CVE') ||
             alertBox.querySelector('.alert-heading').textContent.includes('Security Warning'))) {
            alertBox.classList.add('pulse-effect');
        }
    });
    
    // 添加悬停效果
    const welcomeElement = document.querySelector('.welcome-title');
    if (welcomeElement) {
        welcomeElement.addEventListener('mouseover', function() {
            this.style.transform = 'scale(1.05)';
        });
        
        welcomeElement.addEventListener('mouseout', function() {
            this.style.transform = 'scale(1)';
        });
    }
});

// 主题色变化时更新页面样式
function updateThemeColor(newColor) {
    const welcomeTitle = document.querySelector('.welcome-title');
    if (welcomeTitle) {
        welcomeTitle.style.color = newColor;
    }
}