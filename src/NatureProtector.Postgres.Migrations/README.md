# NatureProtector.Postgres.Migrations

Job exclusivo de schema para local, CI e cloud.

- adquire um advisory lock PostgreSQL;
- aplica as migrations EF Core existentes;
- confirma que não existem migrations pendentes;
- cria/atualiza o role `np_app` sem privilégios administrativos;
- concede apenas DML e utilização das sequences nos schemas funcionais;
- concede leitura mínima de `public.__EFMigrationsHistory` para o bootstrap verificar o schema sem privilégios administrativos.

Não semeia dados e não cria o utilizador administrativo da aplicação.
