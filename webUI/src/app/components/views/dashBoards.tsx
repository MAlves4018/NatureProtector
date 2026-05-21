import { useState, useEffect } from 'react';
import {
  Box, Flex
} from '@chakra-ui/react';
import { AreaRisk, GrafanaStrip } from '../mainComponents/GrafanaStrip';
import { AreaMap } from '../mainComponents/AreaMap';
import { getColors } from '../../utils/utils'
import { AreaCellResponse, SensorNodeResponse } from "../../types"
import { useParams } from 'react-router-dom';
import { api } from '../../services/api';

export function DashBoards({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);

  const { areaCode: areaCodeParam } = useParams<{ areaCode: string }>();
  const [curAreaId, setAreaId] = useState<string>('');

  const [geoJSON, setGeoJSON] = useState<any>(null);
  const [cells, setCells] = useState<AreaCellResponse[]>([]);
  const [sensorNodes, setSensorNodes] = useState<SensorNodeResponse[]>([]);

  useEffect(() => {
    if (areaCodeParam) {
      console.log('Fetching GeoJSON for area:', areaCodeParam);
      api.getAreaGeoJSON(areaCodeParam).then(response => {
        if (!response.id) {
          console.error('Failed to fetch id for area:', areaCodeParam);
          return;
        }
        setAreaId(response.id);
        setGeoJSON(JSON.parse(response.geometryGeoJson || '{}'));
      });
      api.getAreaCells(areaCodeParam).then(response => {
        setCells(response);
        console.log('Cells set:', response);
      }).catch(error => {
        console.error('Failed to fetch cells for area:', areaCodeParam, error);
      });
      api.getAreaSensorNodes(areaCodeParam).then(response => {
        setSensorNodes(response);
      }).catch(error => {
        console.error('Failed to fetch sensor nodes for area:', areaCodeParam, error);
      });
    }
  }, [areaCodeParam]);

  return (
    <Flex direction="column" bg={c.pageBg} transition="background 0.2s" minH="100vh">
      <GrafanaStrip isDark={isDark} areaId={curAreaId} {...c} />
      <AreaRisk isDark={isDark} areaId={curAreaId} {...c} />
      <Box w="100%" h="800px" flexShrink={0}>
        <AreaMap areaId={curAreaId} mapType="standard" showGrid={false} geoJSON={geoJSON} cells={cells} sensorNodes={sensorNodes}/>
      </Box>
    </Flex >
  );
}
