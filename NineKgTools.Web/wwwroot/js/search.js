// 全局搜索相关JavaScript函数

window.focusElement = (selector) => {
    const element = document.querySelector(selector);
    if (element) {
        element.focus();
        return true;
    }
    return false;
};

window.searchHelpers = {
    // 聚焦搜索框
    focusSearchBox: () => {
        return window.focusElement('.search-autocomplete input');
    },
    
    // 监听全局快捷键
    initGlobalShortcuts: () => {
        document.addEventListener('keydown', (e) => {
            // Ctrl+K 或 Cmd+K 打开搜索
            if ((e.ctrlKey || e.metaKey) && e.key === 'k') {
                e.preventDefault();
                window.searchHelpers.focusSearchBox();
            }
            
            // Escape 关闭搜索预览
            if (e.key === 'Escape') {
                const preview = document.querySelector('.modern-search-preview');
                if (preview && preview.style.display !== 'none') {
                    // 触发一个自定义事件来通知Blazor组件
                    const event = new CustomEvent('hideSearchPreview');
                    document.dispatchEvent(event);
                }
            }
        });
    },
    
    // 点击外部关闭搜索预览
    initClickOutside: () => {
        document.addEventListener('click', (e) => {
            const searchWrapper = document.querySelector('.global-search-wrapper');
            if (searchWrapper && !searchWrapper.contains(e.target)) {
                const event = new CustomEvent('hideSearchPreview');
                document.dispatchEvent(event);
            }
        });
    },
    
    // 初始化所有搜索相关功能
    init: () => {
        window.searchHelpers.initGlobalShortcuts();
        window.searchHelpers.initClickOutside();
    }
};

// 页面加载完成后初始化
document.addEventListener('DOMContentLoaded', () => {
    window.searchHelpers.init();
});

// 支持Blazor的互操作
window.blazorCulture = window.blazorCulture || {};
window.blazorCulture.search = window.searchHelpers;