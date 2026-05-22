DO
$$
    BEGIN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'abodio_api') THEN
            CREATE ROLE abodio_api WITH LOGIN;
        END IF;

        -- Grant rds_iam to the role only if the rds_iam role exists (AWS RDS specific)
        IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'rds_iam') THEN
            GRANT rds_iam TO abodio_api;
        END IF;

        GRANT CONNECT ON DATABASE abodio TO abodio_api;
    END
$$;
