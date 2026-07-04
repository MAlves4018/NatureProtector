import { Button, HStack, Icon, Image, VStack } from '@chakra-ui/react';
import { getColors } from '../../utils/utils';
import { useNavigate, useParams } from 'react-router-dom';
import { ChartColumn } from 'lucide-react';

export function DashMain({ isDark }: { isDark: boolean }) {
  const c = getColors(isDark);

  const { areaCode: areaCodeParam } = useParams<{ areaCode: string }>();

  const navigation = useNavigate();

  console.log('isdark:', isDark);
  console.log('colors:', c);
  return (
    <HStack w="100%" h="800px" bg={c.pageBg} justifyContent="center" alignItems="center">
      <VStack
        fontSize="2xl"
        color={c.textPrimary}
        fontWeight="bold"
        borderColor={c.cardBorder}
        borderWidth="6px"
        p={20}
        borderRadius="md"
        alignItems="center"
      >
        <ChartColumn size={100} />
        <h1 style={{ color: c.textPrimary }}>Dashboard for Area </h1>
        <h1 style={{ color: c.redText }}>{areaCodeParam}</h1>
        <Button ml={4} colorScheme="teal" onClick={() => navigation(`/dashboards/${areaCodeParam}/dashNMap`)}>
          View Dashboard and CellGrid Map
        </Button>
      </VStack>
      <VStack
        fontSize="2xl"
        color={c.textPrimary}
        fontWeight="bold"
        borderColor={c.cardBorder}
        borderWidth="6px"
        p={20}
        borderRadius="md"
        alignItems="center"
      >
        <Icon boxSize="100px">
          <Image src="/data-pipeline.png" alt="Pipeline Icon" height="100px" />
        </Icon>
        <h1 style={{ color: c.textPrimary }}>Pipeline for Area </h1>
        <h1 style={{ color: c.redText }}>{areaCodeParam}</h1>
        <Button ml={4} colorScheme="teal" onClick={() => navigation(`/dashboards/${areaCodeParam}/pipeline`)}>
          View Pipeline
        </Button>
      </VStack>
    </HStack>
  );
}
