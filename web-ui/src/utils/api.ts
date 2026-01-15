const TIMEOUT = 5000;

export async function fetchWithTimeout(url: string, options: RequestInit = {}): Promise<Response> {
    const controller = new AbortController();
    const timeoutId = setTimeout(() => controller.abort(), TIMEOUT);

    try {
        const response = await fetch(url, {...options, signal: controller.signal});
        clearTimeout(timeoutId);
        return response;
    } catch (error: any) {
        clearTimeout(timeoutId);
        if (error.name === 'AbortError') throw new Error('请求超时,请重试');
        throw error;
    }
}

export function validateSlug(slug: string): boolean {
    if (!slug || typeof slug !== 'string') return false;
    if (slug.length < 13 || slug.length > 15) return false;
    if (slug.includes('.') || slug.includes('/') || slug.includes('\\')) return false;
    return /^[a-zA-Z0-9_-]{13,15}$/.test(slug);
}

export function getSafeErrorMessage(error: any, statusCode: number): string {
    if (!error) return '操作失败,请重试';

    let errorMsg = typeof error === 'string' ? error : (error.error || error.message || '');

    try {
        errorMsg = errorMsg.replace(/\\u([0-9a-fA-F]{4})/g, (_: string, hex: string) =>
            String.fromCharCode(parseInt(hex, 16))
        );
    } catch (e) {
        console.warn('Failed to decode error message:', errorMsg);
    }

    if (statusCode === 403) {
        if (errorMsg.includes('关闭')) return '任务已关闭,请联系管理员';
        if (errorMsg.includes('过期')) return '任务已过期,提交已截止';
        if (errorMsg.includes('上限')) return errorMsg;
        return '访问被拒绝,请联系管理员';
    }

    if (statusCode === 401) return '密码错误,请重新输入';

    return errorMsg || '操作失败,请重试';
}

export function formatFileSize(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return Math.round((bytes / Math.pow(k, i)) * 100) / 100 + ' ' + sizes[i];
}

export function sanitizeFilename(filename: string): string {
    // 移除路径遍历字符（防止 ../ 攻击）
    let sanitized = filename.replace(/[.]{2,}/g, '.').replace(/^\.+|[\\/]+/g, '');
    // 移除控制字符和特殊字符
    sanitized = sanitized.replace(/[<>:"|?*\x00-\x1F]/g, '_');
    // 限制文件名长度（Windows 限制为 255 字符）
    return sanitized.substring(0, 255);
}

export function formatDate(dateString: string): string {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('zh-CN', {
        year: 'numeric', month: '2-digit', day: '2-digit',
        hour: '2-digit', minute: '2-digit', hour12: false
    }).format(date);
}
