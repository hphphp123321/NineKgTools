// 图片墙响应式布局JS支持
window.photoWallInterop = {
    // 获取当前窗口宽度
    getWindowWidth: function() {
        return window.innerWidth;
    },
    
    // 注册窗口resize事件监听
    onResize: function(dotnetHelper) {
        let resizeTimer;
        const handleResize = function() {
            clearTimeout(resizeTimer);
            // 防抖处理，250ms后执行
            resizeTimer = setTimeout(function() {
                dotnetHelper.invokeMethodAsync('OnWindowResize', window.innerWidth);
            }, 250);
        };
        
        window.addEventListener('resize', handleResize);
        
        // 返回dispose方法供组件销毁时调用
        return {
            dispose: function() {
                window.removeEventListener('resize', handleResize);
                if (resizeTimer) {
                    clearTimeout(resizeTimer);
                }
            }
        };
    },
    
    // 清理事件监听
    dispose: function() {
        // 这个方法会在组件实例中被重写
        console.log('PhotoWall interop disposed');
    }
};