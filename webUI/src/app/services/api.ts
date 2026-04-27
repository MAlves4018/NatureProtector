import {httpClient} from './httpClient';
import {AreaResponse} from '../types';

export const api = {
    getAreas: () => {
        const url = '/control/areas';
        return httpClient.get<AreaResponse[]>(url);
    }
};