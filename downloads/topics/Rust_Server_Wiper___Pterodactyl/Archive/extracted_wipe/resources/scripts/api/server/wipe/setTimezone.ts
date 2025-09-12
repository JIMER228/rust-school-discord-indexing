import http from '@/api/http';

export default (uuid: string, timezone: string): Promise<void> => {
    return new Promise((resolve, reject) => {
        http.post(`/api/client/servers/${uuid}/wipe/timezone`, { timezone })
            .then(() => resolve())
            .catch(reject);
    });
};
