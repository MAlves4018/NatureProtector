import { useEffect, useState } from 'react';
import {
  Box, Flex, Heading
} from '@chakra-ui/react';
import {useColors} from '../../utils/utils';



export function GrafanaStrip({ isDark, areaId, ...c }: { 
  isDark: boolean; 
  areaId: string; 
} 
& ReturnType<typeof useColors>
) {
    const [dashboardLinks, setDashboardLinks] = useState<string[]>([]);
    // Use fetch em um useEffect dentro do componente App
    useEffect(() => {
        console.log("areaId:", areaId);
        fetch('/grafana_dashboards_links.txt')
            .then(r => r.text())
            .then(text => {
            const links = text.split('\n').filter(line => line.trim());
            setDashboardLinks(links);
        })
        .catch(err => console.error('Failed to load dashboards:', err));
    }, []);


    return (
        <Box 
        bg={c.panelBg} 
        borderBottom="1px solid" 
        borderColor={c.panelBorder} 
        flexShrink={0} 
        transition="background 0.2s"
        // REMOVED resize="vertical" to avoid manual height override
      >
        <Box px={6} pt={4} pb={1}>
          <Heading size="xl" color={c.textPrimary} fontFamily="serif">Grafana Dashboards</Heading>
        </Box>

        <Flex
          gap={4} 
          px={6} 
          pb={4} 
          pt={3} 
          overflowX="auto" // Changed from scroll to auto for cleaner look
          align="stretch" // Ensures all boxes have the same height if one is taller
          css={{ 
            scrollbarWidth: 'thin', 
            scrollbarColor: `${isDark ? '#2d3547' : '#d1d5db'} transparent` 
          }}
        >
          {dashboardLinks.map((dash, index) => (
            <Box 
              key={index} 
              minW="450px" 
              flex="1" // Allows the box to grow to fill space
              // REMOVED fixed h="300px"
              // ADDED aspect ratio or a larger default min-height
              minH="500px" 
              borderRadius="md"   
              overflow="hidden"
              border="1px solid"
              borderColor={c.panelBorder}
            >
              <iframe
                src={dash.replace('???', areaId) + '&kiosk'} 
                width="100%"
                height="100%" // Now fills the 500px minH of the parent Box
                style={{ border: 0, display: 'block' }}
                title={`Dashboard ${index}`}
                loading="lazy"
              ></iframe>
            </Box>
          ))}
        </Flex>
      </Box>
      );
    }