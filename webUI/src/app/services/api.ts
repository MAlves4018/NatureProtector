import {httpClient} from './httpClient';
import {AreaCellResponse, AreaGeoJSONResponse, AreaResponse} from '../types';

export const api = {
    getAreas: () => {
        const url = '/control/areas';
        return httpClient.get<AreaResponse[]>(url);
    },

    getAreaGeoJSON: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/geojson`;
        return httpClient.get<AreaGeoJSONResponse>(url); 
    },

    getAreaCells: (areaCode: string) => {
        const url = `/control/areas/${areaCode}/grid-cells`;
        return httpClient.get<AreaCellResponse[]>(url);
    },
};