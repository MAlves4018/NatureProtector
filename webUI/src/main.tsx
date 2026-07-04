import React from 'react';
import ReactDOM from 'react-dom/client';
import { App } from './app/App';
import { TokenProvider } from './app/context/TokenContext';
import './styles/theme.css';
import { ChakraProvider, defaultSystem } from '@chakra-ui/react';

ReactDOM.createRoot(document.getElementById('root')!).render(
  <React.StrictMode>
    <ChakraProvider value={defaultSystem}>
      <TokenProvider>
        <App />
      </TokenProvider>
    </ChakraProvider>
  </React.StrictMode>,
);
