import { useEffect, useRef, useState } from 'react';
import { Box, Flex } from '@chakra-ui/react';
import { AreaRisk, GrafanaStrip } from '../mainComponents/GrafanaStrip';
import { AreaMap } from '../mainComponents/AreaMap';
import { getColors } from '../../utils/utils';
import { AreaCellResponse, SensorNodeResponse } from '../../types';
import { api } from '../../services/api';

export function DashBoards({ isDark, areaCode: areaCodeProp }: { isDark: boolean; areaCode?: string }) {
  const c = getColors(isDark);

  const areaCode = areaCodeProp;
  const [curAreaId, setAreaId] = useState<string>('');

  const [geoJSON, setGeoJSON] = useState<any>(null);
  const [cells, setCells] = useState<AreaCellResponse[]>([]);
  const [sensorNodes, setSensorNodes] = useState<SensorNodeResponse[]>([]);
  const requestGenerationRef = useRef(0);

  useEffect(() => {
    const generation = ++requestGenerationRef.current;
    setAreaId('');
    setGeoJSON(null);
    setCells([]);
    setSensorNodes([]);
    if (!areaCode) return;

    const isCurrent = () => requestGenerationRef.current === generation;
    api
      .getAreaGeoJSON(areaCode)
      .then((response) => {
        if (!isCurrent()) return;
        if (!response.id) {
          console.error('Failed to fetch id for area:', areaCode);
          return;
        }
        try {
          setAreaId(response.id);
          setGeoJSON(JSON.parse(response.geometryGeoJson || '{}'));
        } catch (error) {
          console.error('Failed to parse geometry for area:', areaCode, error);
        }
      })
      .catch((error) => {
        if (isCurrent()) console.error('Failed to fetch geometry for area:', areaCode, error);
      });
    api
      .getAreaCells(areaCode)
      .then((response) => {
        if (isCurrent()) setCells(response);
      })
      .catch((error) => {
        if (isCurrent()) console.error('Failed to fetch cells for area:', areaCode, error);
      });
    api
      .getAreaSensorNodes(areaCode)
      .then((response) => {
        if (isCurrent()) setSensorNodes(response);
      })
      .catch((error) => {
        if (isCurrent()) console.error('Failed to fetch sensor nodes for area:', areaCode, error);
      });
  }, [areaCode]);

  return (
    <Flex direction="column" bg={c.pageBg} transition="background 0.2s" minH="100vh">
      <AreaRisk isDark={isDark} areaId={curAreaId} {...c} />
      <GrafanaStrip isDark={isDark} areaId={curAreaId} {...c} />
      <Box w="100%" h="800px" flexShrink={0}>
        <AreaMap
          areaId={curAreaId}
          mapType="standard"
          showGrid={false}
          geoJSON={geoJSON}
          cells={cells}
          sensorNodes={sensorNodes}
        />
      </Box>
    </Flex>
  );
}
