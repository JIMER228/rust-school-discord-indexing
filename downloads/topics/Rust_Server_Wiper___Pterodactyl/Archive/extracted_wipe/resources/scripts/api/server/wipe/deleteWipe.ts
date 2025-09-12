import http from '@/api/http';

export default (uuid: string, wipe: number): Promise<void> => {
    return new Promise((resolve, reject) => {
        http.delete(`/api/client/servers/${uuid}/wipe/${wipe}`)
            .then(() => resolve())
            .catch(reject);
    });
};
