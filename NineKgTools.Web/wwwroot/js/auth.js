// 认证相关的 JavaScript 函数
window.authInterop = {
    // 登录请求（在浏览器端执行，确保 Cookie 正确设置）
    login: async function (username, password, rememberMe) {
        const formData = new URLSearchParams();
        formData.append('username', username);
        formData.append('password', password);
        formData.append('rememberMe', rememberMe.toString());

        const response = await fetch('/api/auth/login', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: formData,
            credentials: 'same-origin' // 确保 Cookie 被正确处理
        });

        return {
            success: response.ok,
            status: response.status
        };
    },

    // 登出请求
    logout: async function () {
        const response = await fetch('/api/auth/logout', {
            method: 'POST',
            credentials: 'same-origin'
        });

        return {
            success: response.ok,
            status: response.status
        };
    },

    // 修改用户名（在浏览器端执行，确保 Cookie 被带上通过 [Authorize] 校验）
    changeUsername: async function (currentPassword, newUsername) {
        const formData = new URLSearchParams();
        formData.append('currentPassword', currentPassword);
        formData.append('newUsername', newUsername);

        const response = await fetch('/api/auth/change-username', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: formData,
            credentials: 'same-origin'
        });

        let message = '';
        try {
            const data = await response.json();
            message = data?.message ?? '';
        } catch { /* 空 body 或非 JSON，保持默认 */ }

        return {
            success: response.ok,
            status: response.status,
            message: message
        };
    },

    // 修改密码
    changePassword: async function (currentPassword, newPassword) {
        const formData = new URLSearchParams();
        formData.append('currentPassword', currentPassword);
        formData.append('newPassword', newPassword);

        const response = await fetch('/api/auth/change-password', {
            method: 'POST',
            headers: {
                'Content-Type': 'application/x-www-form-urlencoded'
            },
            body: formData,
            credentials: 'same-origin'
        });

        let message = '';
        try {
            const data = await response.json();
            message = data?.message ?? '';
        } catch { /* 空 body 或非 JSON，保持默认 */ }

        return {
            success: response.ok,
            status: response.status,
            message: message
        };
    }
};
