import React, { useEffect, useState } from 'react';
import { Box, Flex, Text } from '@chakra-ui/react';
import { Sun, Moon, LogIn } from 'lucide-react';
import { getColors } from '../../utils/utils';
import { useToken } from '../../context/TokenContext';
import { UserModal } from '../components/UserModal';
import npLogoUrl from '../../../../images/NPIconNoBg.png';
import { useUiCapabilities, useUiLocale } from '../../state';
import { useNavigate } from 'react-router-dom';

export function NavBar({
  isDark,
  setIsDark,
}: {
  isDark: boolean;
  setIsDark: React.Dispatch<React.SetStateAction<boolean>>;
}) {
  const { copy, locale, setLocale } = useUiLocale();
  const c = getColors(isDark);
  const { user, token, refreshToken } = useToken();
  const signedIn = Boolean(token);
  const [isUserModalOpen, setIsUserModalOpen] = useState(false);
  const { isPublic, capabilityAuthority, capabilitiesLoading } =
    useUiCapabilities();
  useEffect(() => {
    if (signedIn && !user) {
      refreshToken();
    }
  }, [signedIn, user, refreshToken]);
  const navigate = useNavigate();

  return (
    <>
      <Flex
        as="nav"
        align="center"
        gap={3}
        px={6}
        py="10px"
        bg={c.navBg}
        borderBottom="1px solid"
        borderColor={c.navBorder}
        shadow="sm"
        flexShrink={0}
        transition="background 0.2s"
      >
        <img
          src={npLogoUrl}
          width="36"
          height="36"
          alt="Nature Protector"
          style={{ display: 'block', objectFit: 'contain' }}
        />
        <Text fontWeight="semibold" fontSize="lg" color={c.textPrimary} letterSpacing="wide">
          Nature Protector
        </Text>
        <Box flex={1} />
        <Box
          as="button"
          onClick={() => setIsUserModalOpen(true)}
          display="flex"
          alignItems="center"
          gap="8px"
          px={3}
          py="6px"
          borderRadius="full"
          bg={c.sectionBg}
          border="1px solid"
          borderColor={c.navBorder}
          cursor="pointer"
          transition="all 0.2s"
          _hover={{ opacity: 0.9 }}
        >
          <Box
            width="26px"
            height="26px"
            borderRadius="999px"
            display="grid"
            placeItems="center"
            fontWeight={700}
            fontSize="xs"
            color={c.textPrimary}
            bg={signedIn ? '#22c55e' : c.toggleBg}
          >
            {signedIn ? user?.fullName?.[0] || user?.username?.[0] || 'U' : '?'}
          </Box>
          <Text fontSize="xs" color={c.textSecond}>
            {signedIn ? `Signed in${user?.fullName ? ` as ${user.fullName}` : ''}` : 'Not signed in'}
          </Text>
        </Box>
        <Box
          as="button"
          onClick={() => setIsDark((d) => !d)}
          display="flex"
          alignItems="center"
          gap="6px"
          px={3}
          py="6px"
          borderRadius="full"
          bg={c.toggleBg}
          border="1px solid"
          borderColor={c.navBorder}
          cursor="pointer"
          transition="all 0.2s"
          _hover={{ opacity: 0.8 }}
        >
          {isDark ? (
            <>
              <Sun size={14} color="#f59e0b" />
              <Text fontSize="xs" color={c.textPrimary}>
                Light
              </Text>
            </>
          ) : (
            <>
              <Moon size={14} color="#6366f1" />
              <Text fontSize="xs" color={c.textPrimary}>
                Dark
              </Text>
            </>
          )}
        </Box>
        <UserModal isDark={isDark} user={user} isOpen={isUserModalOpen} onClose={() => setIsUserModalOpen(false)} />F
      </Flex>
      <header className="ui-hero">
        <div>
          <p className="ui-kicker">
            {copy('app.prototype')} / {copy('app.readOnly')}
          </p>
          <h1 className="ui-title">{copy('app.name')}</h1>
          <p className="ui-lead">
            {isPublic
              ? 'Entrada pública orientada ao produto: propósito, limites e estado dos dados sem superfícies internas.'
              : `Perfil ativo: ${user?.roles.join(', ') || 'sem funções'}. Autorização: ${capabilitiesLoading ? 'a validar no backend' : capabilityAuthority
              }.`}
          </p>
        </div>
        <div className="ui-hero-actions">
          <div className="ui-language">
            <button
              type="button"
              className={locale === 'pt-PT' ? 'ui-button' : 'ui-secondary'}
              onClick={() => setLocale('pt-PT')}
            >
              {copy('language.pt')}
            </button>
            <button
              type="button"
              className={locale === 'en' ? 'ui-button' : 'ui-secondary'}
              onClick={() => setLocale('en')}
            >
              {copy('language.en')}
            </button>
          </div>
          {isPublic && (
            <button type="button" className="ui-button" onClick={() => navigate('/login')}>
              <LogIn size={16} />
              {copy('nav.login')}
            </button>
          )}
        </div>
      </header>
    </>
  );
}
